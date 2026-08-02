using LynqMentrics.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LynqMentrics.Data;

public static class DemoDataSeeder
{
    private const string DemoEmail = "demo@lynqmentrics.com";
    private const string DemoUserName = "demo";
    private const string DemoPassword = "Demo12345!";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();

        var existingDemoUser = await userManager.FindByEmailAsync(DemoEmail);
        if (existingDemoUser is not null)
        {
            return;
        }

        var demoUser = new AppUser
        {
            UserName = DemoUserName,
            Email = DemoEmail,
            EmailConfirmed = true,
            IsPro = false
        };

        var createUserResult = await userManager.CreateAsync(demoUser, DemoPassword);
        if (!createUserResult.Succeeded)
        {
            var errors = string.Join("; ", createUserResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException($"Could not create demo user. {errors}");
        }

        var links = new[]
        {
            new Link
            {
                UserId = demoUser.Id,
                ShortCode = "demo1",
                OriginalUrl = "https://www.producthunt.com/",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new Link
            {
                UserId = demoUser.Id,
                ShortCode = "demo2",
                OriginalUrl = "https://github.com/trending",
                CreatedAt = DateTime.UtcNow.AddDays(-9)
            },
            new Link
            {
                UserId = demoUser.Id,
                ShortCode = "demo3",
                OriginalUrl = "https://www.notion.so/templates",
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new Link
            {
                UserId = demoUser.Id,
                ShortCode = "demo4",
                OriginalUrl = "https://news.ycombinator.com/",
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            }
        };

        await dbContext.Links.AddRangeAsync(links);

        var referrers = new string?[]
        {
            "facebook.com",
            "linkedin.com",
            "google.com",
            "github.com",
            null,
            "medium.com",
            "twitter.com",
            "news.ycombinator.com",
            "instagram.com",
            "tiktok.com"
        };

        var countries = new string?[] { "US", "UK", "DE", "IN", null };
        var devices = new[] { "Mobile", "Desktop" };
        var browsers = new[] { "Chrome", "Safari", "Firefox", "Edge" };
        var clickCount = Random.Shared.Next(30, 51);
        var utcNow = DateTime.UtcNow;

        var clicks = new List<Click>(clickCount);
        for (var i = 0; i < clickCount; i++)
        {
            var link = links[Random.Shared.Next(links.Length)];
            var clickedAt = utcNow
                .AddDays(-Random.Shared.Next(0, 7))
                .AddHours(-Random.Shared.Next(0, 24))
                .AddMinutes(-Random.Shared.Next(0, 60))
                .AddSeconds(-Random.Shared.Next(0, 60));

            var browser = browsers[Random.Shared.Next(browsers.Length)];
            var device = devices[Random.Shared.Next(devices.Length)];

            clicks.Add(new Click
            {
                Link = link,
                ClickedAt = clickedAt,
                Referrer = referrers[Random.Shared.Next(referrers.Length)],
                Country = countries[Random.Shared.Next(countries.Length)],
                Device = device,
                Browser = browser,
                UserAgent = $"{browser} Demo/{Random.Shared.Next(100, 140)} ({device})"
            });
        }

        await dbContext.Clicks.AddRangeAsync(clicks);
        await dbContext.SaveChangesAsync();
    }
}
