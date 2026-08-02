using LynqMentrics.Data;
using Microsoft.EntityFrameworkCore;

namespace LynqMentrics.Services;

public class AnalyticsService(AppDbContext dbContext)
{
    public async Task<LinkStatsResponse?> GetLinkStatsAsync(string userId, Guid linkId, CancellationToken cancellationToken)
    {
        var link = await dbContext.Links
            .AsNoTracking()
            .Include(l => l.Clicks)
            .FirstOrDefaultAsync(l => l.Id == linkId && l.UserId == userId, cancellationToken);

        if (link is null)
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;
        var today = utcNow.Date;
        var firstDay = today.AddDays(-6);

        var clicksByDate = link.Clicks
            .Where(c => c.ClickedAt.Date >= firstDay)
            .GroupBy(c => c.ClickedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var last7Days = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = firstDay.AddDays(offset);
                return new DailyClicks(date.ToString("yyyy-MM-dd"), clicksByDate.GetValueOrDefault(date, 0));
            })
            .ToList();

        var clicksToday = link.Clicks.Count(c => c.ClickedAt.Date == today);
        var clicksThisWeek = last7Days.Sum(day => day.Clicks);

        return new LinkStatsResponse(
            link.Id,
            link.ShortCode,
            link.OriginalUrl,
            link.Clicks.Count,
            clicksToday,
            clicksThisWeek,
            last7Days,
            BuildTopSources(link.Clicks.Select(c => c.Referrer), "Direct"),
            BuildTopCountries(link.Clicks.Select(c => c.Country)),
            BuildTopDevices(link.Clicks.Select(c => c.Device)),
            BuildTopBrowsers(link.Clicks.Select(c => c.Browser))
        );
    }

    private static IReadOnlyList<SourceCount> BuildTopSources(IEnumerable<string?> values, string fallback) =>
        values
            .Select(v => string.IsNullOrWhiteSpace(v) ? fallback : v)
            .GroupBy(v => v!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new SourceCount(g.Key, g.Count()))
            .ToList();

    private static IReadOnlyList<CountryCount> BuildTopCountries(IEnumerable<string?> values) =>
        values
            .Select(v => string.IsNullOrWhiteSpace(v) ? "Unknown" : v)
            .GroupBy(v => v!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new CountryCount(g.Key, g.Count()))
            .ToList();

    private static IReadOnlyList<DeviceCount> BuildTopDevices(IEnumerable<string?> values) =>
        values
            .Select(v => string.IsNullOrWhiteSpace(v) ? "Unknown" : v)
            .GroupBy(v => v!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new DeviceCount(g.Key, g.Count()))
            .ToList();

    private static IReadOnlyList<BrowserCount> BuildTopBrowsers(IEnumerable<string?> values) =>
        values
            .Select(v => string.IsNullOrWhiteSpace(v) ? "Unknown" : v)
            .GroupBy(v => v!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new BrowserCount(g.Key, g.Count()))
            .ToList();
}

public sealed record LinkStatsResponse(
    Guid LinkId,
    string ShortCode,
    string OriginalUrl,
    int TotalClicks,
    int ClicksToday,
    int ClicksThisWeek,
    IReadOnlyList<DailyClicks> Last7Days,
    IReadOnlyList<SourceCount> TopReferrers,
    IReadOnlyList<CountryCount> TopCountries,
    IReadOnlyList<DeviceCount> TopDevices,
    IReadOnlyList<BrowserCount> TopBrowsers);

public sealed record DailyClicks(string Date, int Clicks);
public sealed record SourceCount(string Source, int Count);
public sealed record CountryCount(string Country, int Count);
public sealed record DeviceCount(string Device, int Count);
public sealed record BrowserCount(string Browser, int Count);
