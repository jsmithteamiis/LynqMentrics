# LynqMentrics

[![Repository](https://img.shields.io/badge/repo-jsmithteamiis%2FLynqMentrics-181717?logo=github)](https://github.com/jsmithteamiis/LynqMentrics)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![CI](https://github.com/jsmithteamiis/LynqMentrics/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/jsmithteamiis/LynqMentrics/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Add%20LICENSE-orange)](https://github.com/jsmithteamiis/LynqMentrics/blob/main/LICENSE)
[![Last Commit](https://img.shields.io/github/last-commit/jsmithteamiis/LynqMentrics)](https://github.com/jsmithteamiis/LynqMentrics/commits/main)
[![Open Issues](https://img.shields.io/github/issues/jsmithteamiis/LynqMentrics)](https://github.com/jsmithteamiis/LynqMentrics/issues)

**Short links. Deep insights.**

LynqMentrics is a full-stack URL shortener + analytics web app built with **ASP.NET Core (.NET 10)** using **Razor Pages**, **Minimal APIs**, **ASP.NET Core Identity**, and **EF Core**.

## Features

- User authentication with ASP.NET Core Identity
- Google OAuth sign-in support
- Create short links from long URLs
- Optional custom slug support
- Dashboard to list, copy, delete, and inspect links
- Redirect tracking with click analytics:
  - Timestamp
  - Referrer
  - User-Agent
  - IP hash (SHA256 + salt)
  - Device type
  - Browser type
  - Country (if header provided)
- Analytics view per link:
  - Total clicks
  - Clicks today
  - Clicks this week
  - Last 7 days line chart
  - Top referrers/countries/devices/browsers
- Free-tier limit: **50 links/user** (non-pro users)

## Tech Stack

- .NET 10
- ASP.NET Core Razor Pages + Minimal APIs
- ASP.NET Core Identity
- Entity Framework Core 10
- SQLite (local development)
- PostgreSQL / Supabase (production)
- Tailwind CSS (CDN)
- Chart.js (CDN)

## Project Structure

```text
LynqMentrics/
├── LynqMentrics.slnx
└── LynqMentrics/
    ├── Areas/Identity/
    ├── Data/
    │   └── AppDbContext.cs
    ├── Migrations/
    ├── Models/
    │   ├── AppUser.cs
    │   ├── Link.cs
    │   └── Click.cs
    ├── Pages/
    │   ├── Index.cshtml
    │   └── Dashboard/
    │       ├── Index.cshtml
    │       └── Analytics.cshtml
    ├── Services/
    │   ├── ShortCodeGenerator.cs
    │   └── AnalyticsService.cs
    ├── Program.cs
    ├── appsettings.json
    └── LynqMentrics.csproj
```

## Requirements

- .NET SDK 10.x

## Local Development Setup

1. Clone and open the repository.
2. From repository root:

```powershell
dotnet restore .\LynqMentrics\LynqMentrics.csproj
dotnet build .\LynqMentrics\LynqMentrics.csproj
```

3. Apply database migrations:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef database update --project .\LynqMentrics\LynqMentrics.csproj
```

4. Run:

```powershell
dotnet run --project .\LynqMentrics\LynqMentrics.csproj
```

## Configuration

Edit `LynqMentrics/appsettings.json`:

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=lynqmentrics.db",
    "PostgresConnection": "Host=YOUR_SUPABASE_HOST;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  },
  "IpHashSalt": "LynqMentrics_MVP_Static_Salt_2026"
}
```

### Database provider switch

- Local SQLite: `"DatabaseProvider": "Sqlite"`
- Supabase/Postgres: `"DatabaseProvider": "Postgres"`

## Google OAuth Setup

1. Go to Google Cloud Console.
2. Create OAuth 2.0 credentials (Web application).
3. Add authorized redirect URI:
   - `https://localhost:7054/signin-google`
4. Place the client ID/secret into `Authentication:Google`.

## Minimal API Endpoints

- `POST /api/links` (auth required)
- `GET /api/links` (auth required)
- `DELETE /api/links/{id}` (auth required)
- `GET /api/links/{id}/stats` (auth required)

## Redirect Route

- `GET /{shortCode}`  
Looks up the short code, records click analytics asynchronously, and redirects to the original URL.

## Notes

- Identity UI pages are scaffolded under `Areas/Identity/Pages`.
- The app applies EF migrations on startup.
- For first-time setup in a new environment, still run `dotnet ef database update` as part of deployment.

## Deployment (Supabase + Production Hosting)

### 1) Create PostgreSQL database in Supabase

1. Create a Supabase project.
2. In **Project Settings > Database**, copy connection details.
3. Set your production connection string as:

```text
Host=YOUR_SUPABASE_HOST;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true
```

### 2) Set production configuration

Set these environment variables in your hosting platform:

- `DatabaseProvider=Postgres`
- `ConnectionStrings__PostgresConnection=<your_supabase_connection_string>`
- `Authentication__Google__ClientId=<google_client_id>`
- `Authentication__Google__ClientSecret=<google_client_secret>`
- `IpHashSalt=<strong_random_salt>`
- `ASPNETCORE_ENVIRONMENT=Production`

### 3) Apply migrations in production

Run once during deployment:

```powershell
dotnet ef database update --project .\LynqMentrics\LynqMentrics.csproj
```

### 4) Host the app

You can deploy to any ASP.NET-compatible host (Azure App Service, Render, Fly.io, Railway, Docker/Kubernetes).

Recommended production command:

```powershell
dotnet publish .\LynqMentrics\LynqMentrics.csproj -c Release -o .\publish
```

Then run the published app on the server:

```powershell
dotnet .\publish\LynqMentrics.dll
```

## License

Add your preferred license (MIT, Apache-2.0, etc.).
