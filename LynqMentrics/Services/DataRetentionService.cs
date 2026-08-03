using LynqMentrics.Data;
using Microsoft.EntityFrameworkCore;

namespace LynqMentrics.Services;

/// <summary>
/// Periodically enforces the data retention policy required by GDPR/CCPA:
/// deletes click analytics older than the configured retention window and
/// clears legacy raw user-agent strings (data minimization).
/// </summary>
public sealed class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DataRetentionService> logger) : BackgroundService
{
    private const int DefaultRetentionDays = 365;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Compliance:DataRetentionEnabled"))
        {
            logger.LogInformation("Data retention service is disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(configuration.GetValue<double>("Compliance:DataRetentionIntervalHours", 24));

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunRetentionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Data retention run failed.");
            }
        }
    }

    internal async Task RunRetentionAsync(CancellationToken cancellationToken)
    {
        var retentionDays = configuration.GetValue<int>("Compliance:DataRetentionDays", DefaultRetentionDays);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Purge click analytics older than the retention window. Click rows only
        //    carry pseudonymized (hashed IP) or aggregated data, but referrer and
        //    legacy user-agent values can still contain PII, so they must expire.
        var expiredClicks = await dbContext.Clicks
            .Where(click => click.ClickedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (expiredClicks > 0)
        {
            logger.LogInformation("Retention: purged {Count} click record(s) older than {Days} day(s).",
                expiredClicks, retentionDays);
        }

        // 2. Data minimization: clear raw user-agent strings captured before they
        //    were no longer stored. Device/Browser aggregates remain available.
        var clearedUserAgents = await dbContext.Clicks
            .Where(click => click.UserAgent != null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(click => click.UserAgent, (string?)null),
                cancellationToken);

        if (clearedUserAgents > 0)
        {
            logger.LogInformation("Retention: cleared {Count} legacy user-agent value(s).", clearedUserAgents);
        }
    }
}
