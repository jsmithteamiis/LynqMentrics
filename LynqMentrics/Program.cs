using System.Security.Cryptography;
using System.Text;
using LynqMentrics.Data;
using LynqMentrics.Models;
using LynqMentrics.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services
    .AddDefaultIdentity<AppUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>();

var databaseProvider = builder.Configuration["DatabaseProvider"] ??
                       (builder.Environment.IsDevelopment() ? "Sqlite" : "Postgres");

if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var pgConnection = builder.Configuration.GetConnectionString("PostgresConnection")
                       ?? throw new InvalidOperationException("Missing Postgres connection string.");
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(pgConnection));
}
else
{
    var sqliteConnection = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException("Missing SQLite connection string.");
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqliteConnection));
}

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

builder.Services.AddScoped<ShortCodeGenerator>();
builder.Services.AddScoped<AnalyticsService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
});

var app = builder.Build();

await using (var startupScope = app.Services.CreateAsyncScope())
{
    var dbContext = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

var linksApi = app.MapGroup("/api/links").RequireAuthorization();

linksApi.MapPost("/", async (
    CreateLinkRequest requestBody,
    HttpContext httpContext,
    AppDbContext dbContext,
    UserManager<AppUser> userManager,
    ShortCodeGenerator shortCodeGenerator,
    CancellationToken cancellationToken) =>
{
    var userId = userManager.GetUserId(httpContext.User);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var user = await userManager.FindByIdAsync(userId);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (!Uri.TryCreate(requestBody.OriginalUrl, UriKind.Absolute, out var parsedUri) ||
        (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.BadRequest(new { error = "Please provide a valid http/https URL." });
    }

    var currentLinkCount = await dbContext.Links.CountAsync(link => link.UserId == userId, cancellationToken);
    if (!user.IsPro && currentLinkCount >= 50)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var selectedCode = requestBody.CustomSlug?.Trim();
    if (!string.IsNullOrWhiteSpace(selectedCode))
    {
        if (!IsValidCustomSlug(selectedCode))
        {
            return Results.BadRequest(new { error = "Custom slug can only contain letters, numbers, '-' and '_'." });
        }

        var slugExists = await dbContext.Links.AnyAsync(link => link.ShortCode == selectedCode, cancellationToken);
        if (slugExists)
        {
            return Results.Conflict(new { error = "This custom slug is already taken." });
        }
    }
    else
    {
        selectedCode = await GenerateUniqueShortCodeAsync(dbContext, shortCodeGenerator, cancellationToken);
    }

    var link = new Link
    {
        UserId = userId,
        OriginalUrl = parsedUri.ToString(),
        ShortCode = selectedCode,
        CreatedAt = DateTime.UtcNow
    };

    dbContext.Links.Add(link);
    await dbContext.SaveChangesAsync(cancellationToken);

    var shortUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{link.ShortCode}";
    return Results.Ok(new { shortUrl, shortCode = link.ShortCode, linkId = link.Id });
});

linksApi.MapGet("/", async (
    HttpContext httpContext,
    AppDbContext dbContext,
    UserManager<AppUser> userManager,
    CancellationToken cancellationToken) =>
{
    var userId = userManager.GetUserId(httpContext.User);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var links = await dbContext.Links
        .AsNoTracking()
        .Where(link => link.UserId == userId)
        .OrderByDescending(link => link.CreatedAt)
        .Select(link => new
        {
            link.Id,
            link.ShortCode,
            link.OriginalUrl,
            link.CreatedAt,
            ClicksCount = link.Clicks.Count
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(links);
});

linksApi.MapDelete("/{id:guid}", async (
    Guid id,
    HttpContext httpContext,
    AppDbContext dbContext,
    UserManager<AppUser> userManager,
    CancellationToken cancellationToken) =>
{
    var userId = userManager.GetUserId(httpContext.User);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var link = await dbContext.Links.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    if (link is null)
    {
        return Results.NotFound();
    }

    if (link.UserId != userId)
    {
        return Results.Forbid();
    }

    dbContext.Links.Remove(link);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

linksApi.MapGet("/{id:guid}/stats", async (
    Guid id,
    HttpContext httpContext,
    UserManager<AppUser> userManager,
    AnalyticsService analyticsService,
    CancellationToken cancellationToken) =>
{
    var userId = userManager.GetUserId(httpContext.User);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var stats = await analyticsService.GetLinkStatsAsync(userId, id, cancellationToken);
    return stats is null ? Results.NotFound() : Results.Ok(stats);
});

app.MapGet("/{shortCode:regex(^[a-zA-Z0-9_-]+$)}", async (
    string shortCode,
    HttpContext httpContext,
    IServiceScopeFactory scopeFactory,
    ILoggerFactory loggerFactory,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    await using var scope = scopeFactory.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var link = await dbContext.Links
        .AsNoTracking()
        .FirstOrDefaultAsync(l => l.ShortCode == shortCode, cancellationToken);

    if (link is null)
    {
        return Results.NotFound();
    }

    var referrer = httpContext.Request.Headers.Referer.ToString();
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
    var country = httpContext.Request.Headers["X-Verified-Country"].ToString();
    var ipSalt = configuration["IpHashSalt"] ?? "LynqMentrics_MVP_Static_Salt_2026";
    var logger = loggerFactory.CreateLogger("ClickRecorder");

    var clickTask = RecordClickAsync(
        scopeFactory,
        link.Id,
        referrer,
        userAgent,
        remoteIp,
        string.IsNullOrWhiteSpace(country) ? null : country,
        ipSalt,
        CancellationToken.None);

    _ = clickTask.ContinueWith(task =>
    {
        logger.LogError(task.Exception, "Failed to record click for link {LinkId}", link.Id);
    }, TaskContinuationOptions.OnlyOnFaulted);

    return Results.Redirect(link.OriginalUrl);
});

app.Run();

static bool IsValidCustomSlug(string slug)
{
    foreach (var ch in slug)
    {
        if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_')
        {
            return false;
        }
    }

    return slug.Length is >= 3 and <= 64;
}

static async Task<string> GenerateUniqueShortCodeAsync(
    AppDbContext dbContext,
    ShortCodeGenerator shortCodeGenerator,
    CancellationToken cancellationToken)
{
    for (var attempts = 0; attempts < 20; attempts++)
    {
        var code = shortCodeGenerator.Generate();
        var exists = await dbContext.Links.AnyAsync(link => link.ShortCode == code, cancellationToken);
        if (!exists)
        {
            return code;
        }
    }

    throw new InvalidOperationException("Could not generate a unique short code. Please retry.");
}

static async Task RecordClickAsync(
    IServiceScopeFactory scopeFactory,
    Guid linkId,
    string? referrer,
    string? userAgent,
    string? remoteIpAddress,
    string? country,
    string salt,
    CancellationToken cancellationToken)
{
    await using var scope = scopeFactory.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var click = new Click
    {
        LinkId = linkId,
        ClickedAt = DateTime.UtcNow,
        Referrer = string.IsNullOrWhiteSpace(referrer) ? null : referrer,
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
        IpHash = CreateIpHash(remoteIpAddress, salt),
        Country = country,
        Device = ParseDevice(userAgent),
        Browser = ParseBrowser(userAgent)
    };

    dbContext.Clicks.Add(click);
    await dbContext.SaveChangesAsync(cancellationToken);
}

static string? CreateIpHash(string? ipAddress, string salt)
{
    if (string.IsNullOrWhiteSpace(ipAddress))
    {
        return null;
    }

    var bytes = Encoding.UTF8.GetBytes($"{ipAddress}:{salt}");
    return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

static string ParseDevice(string? userAgent)
{
    if (string.IsNullOrWhiteSpace(userAgent))
    {
        return "Desktop";
    }

    return userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "Mobile" : "Desktop";
}

static string ParseBrowser(string? userAgent)
{
    if (string.IsNullOrWhiteSpace(userAgent))
    {
        return "Other";
    }

    if (userAgent.Contains("Edg", StringComparison.OrdinalIgnoreCase))
    {
        return "Edge";
    }

    if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
    {
        return "Chrome";
    }

    if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
    {
        return "Firefox";
    }

    if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase))
    {
        return "Safari";
    }

    if (userAgent.Contains("OPR", StringComparison.OrdinalIgnoreCase) ||
        userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
    {
        return "Opera";
    }

    return "Other";
}

internal sealed record CreateLinkRequest(string OriginalUrl, string? CustomSlug);
