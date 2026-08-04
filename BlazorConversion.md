# LynqMentrics — Syncfusion Blazor Conversion

> Status: **Planned** · Target framework: `net10.0` · Syncfusion version: **33.2.15** (latest)
> This document captures the plan for migrating the LynqMentrics UI from **Razor Pages + Minimal APIs** to a **Blazor Web App (interactive Server)** built with **Syncfusion Blazor** components.

---

## 1. Overview

LynqMentrics is a URL shortener + click-analytics web app built on ASP.NET Core (.NET 10).
The current UI layer uses Razor Pages with a JavaScript-driven dashboard (fetch calls to Minimal API
endpoints, SignalR client, Chart.js). This conversion replaces that UI layer with Syncfusion Blazor
components while **reusing** the existing backend: models, `AppDbContext`, services, Identity/Google
OAuth, and the redirect/click-tracking endpoint.

### Goals

- Modernize the UI with Syncfusion enterprise components (DataGrid, Charts, Inputs, Popups, Notifications).
- Remove hand-written dashboard JavaScript and the Chart.js dependency.
- Keep the real-time experience (SignalR) working inside the Blazor circuit.
- Preserve the current dark custom theme and the GDPR/CCPA consent flow.

---

## 2. Current vs. Target Architecture

| Concern | Current | Target |
|---|---|---|
| UI framework | Razor Pages (`Pages/`) | Blazor Web App, interactive **Server** render mode (`Components/`) |
| Links table (dashboard) | Hand-rolled HTML table + JS fetch | **SfGrid** (Syncfusion DataGrid) |
| Analytics charts | Chart.js (CDN) | **SfChart** (line, pie/bar) |
| Create-link form | Razor form | **SfTextBox** + **SfAutoComplete** (slug suggestion) |
| Notifications / confirmations | Inline feedback / `confirm()` | **SfToast**, **SfDialog**, **SfTooltip** |
| Realtime | JS SignalR client (`@microsoft/signalr` CDN) | Server-side `HubConnection` (SignalR client lib) inside the Blazor circuit |
| Styling | Tailwind (CDN) + custom `brand*` colors | Tailwind layout preserved + Syncfusion **tailwind3-dark** theme + CSS overrides |
| Hosting endpoints | `MapRazorPages()` | `MapRazorComponents<App>().AddInteractiveServerRenderMode()` |
| API endpoints (`/api/links`, `/api/privacy`) | Kept | **Kept** (unchanged) |
| Redirect + click tracking (`GET /{shortCode}`) | Minimal API catch-all | **Kept** (unchanged) |
| Auth | Identity + Google OAuth, cookie | Identity + Google OAuth, cookie + `AddCascadingAuthenticationState` |

---

## 3. Key Decisions

1. **Hosting model** — Blazor Web App with interactive Server render mode. Closest to the current
   architecture (server-rendered, SignalR transport), easiest migration path, full Syncfusion support.
2. **Scope** — Full UI migration (all Razor Pages → Blazor components). The Minimal API surface
   (`/api/links`, `/api/privacy`) and the `/{shortCode}` redirect stay as endpoints.
3. **Syncfusion components** — DataGrid (dashboard), Charts (analytics), Toast/Dialog/Tooltip,
   TextBox/AutoComplete (create-link form).
4. **Theme** — Keep the current dark slate look. Use the Syncfusion `tailwind3-dark` theme and
   override with the existing `brandCyan` / `brandIndigo` / `brandEmerald` palette.
5. **License** — Register `SyncfusionLicenseProvider.RegisterLicense` from configuration
   (`Syncfusion:LicenseKey`) with a placeholder value (Syncfusion shows a banner without a valid key).

---

## 4. NuGet Packages

Add to `LynqMentrics/LynqMentrics.csproj` (all `33.2.15`):

```xml
<ItemGroup>
  <!-- Syncfusion Blazor (granular packages — smaller footprint than the meta package) -->
  <PackageReference Include="Syncfusion.Blazor.Grid" Version="33.2.15" />
  <PackageReference Include="Syncfusion.Blazor.Charts" Version="33.2.15" />
  <PackageReference Include="Syncfusion.Blazor.Inputs" Version="33.2.15" />
  <PackageReference Include="Syncfusion.Blazor.Popups" Version="33.2.15" />
  <PackageReference Include="Syncfusion.Blazor.Notifications" Version="33.2.15" />
  <PackageReference Include="Syncfusion.Blazor.Themes" Version="33.2.15" />
  <!-- Server-side SignalR client for realtime inside the Blazor circuit -->
  <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.0" />
</ItemGroup>
```

> Alternative: a single `Syncfusion.Blazor` meta package (33.2.15) is simpler but pulls in all
> component assemblies.

`appsettings.json` addition:

```json
"Syncfusion": {
  "LicenseKey": ""
}
```

---

## 5. Migration Phases

### Phase 0 — Baseline & package setup

1. Add the Syncfusion package references above.
2. Add `Syncfusion:LicenseKey` to configuration; register it in `Program.cs`:
   ```csharp
   var licenseKey = builder.Configuration["Syncfusion:LicenseKey"];
   if (!string.IsNullOrWhiteSpace(licenseKey))
       SyncfusionLicenseProvider.RegisterLicense(licenseKey);
   ```
3. Register the Blazor component services:
   ```csharp
   builder.Services.AddRazorComponents()
                   .AddInteractiveServerComponents();
   ```

### Phase 1 — Blazor shell (replaces Razor Pages hosting)

4. Add `Components/` with:
   - `_Imports.razor` — global usings (`LynqMentrics.Services`, Syncfusion namespaces, auth)
   - `App.razor` — root component (head/link styles, `Routes` + `HeadOutlet`)
   - `Routes.razor` — `<Router>` with `<AuthorizeRouteView>`
   - `MainLayout.razor` — port of `Pages/Shared/_Layout.cshtml` (nav bar, consent banner, footer, dark theme)
5. `Program.cs` changes:
   - Replace `MapRazorPages()` with `MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
   - Add `builder.Services.AddCascadingAuthenticationState();`
   - Keep auth middleware order: `UseAuthentication` → `UseAuthorization`.
   - Keep `MapHub<DashboardHub>("/hubs/dashboard")`, `/api/*` groups, and the `/{shortCode}` catch-all.
   - Routing note: endpoint routing prefers literal segments over the `{shortCode}` parameter, so
     Blazor page routes (`/dashboard`, `/analytics/{id}`, …) still resolve; unknown slugs still hit
     the redirect endpoint (same behavior as today).
6. Add Syncfusion assets to the layout (respecting the existing consent gate for third-party requests):
   - Theme: `_content/Syncfusion.Blazor.Themes/tailwind3-dark.css`
   - Script: `https://blazor.syncfusion.com/scripts/syncfusion-blazor.min.js` (or bundled via `wwwroot`)

### Phase 2 — Dashboard (links list)

7. New `Components/Pages/Dashboard.razx` (`[Authorize]`, `InteractiveServer`):
   - **SfGrid** bound to the current user's links (via `AnalyticsService` / `AppDbContext`; replaces
     the `fetch('/api/links')` JS).
   - Columns: short link (copy button), original URL (decrypted through `PiiTokenizationService`),
     click count, created date; row actions: **copy**, **delete** (SfDialog confirm), **view analytics**.
   - Summary cards (Total Links, Total Clicks, Status/Last updated) fed from the same data.
   - **SfToast** for success/error feedback.
8. Realtime: create a scoped service that opens a server-side `HubConnection` to `/hubs/dashboard`,
   joins the `user:{userId}` group, and raises `LinkStatsChanged` / `LinksChanged` events to trigger
   `StateHasChanged()`. This replaces the old JS SignalR client and the `@microsoft/signalr` CDN.

### Phase 3 — Analytics

9. New `Components/Pages/Analytics.razx` (auth'd, interactive Server):
   - **SfChart** line chart for the last-7-days click series.
   - **SfChart** pie/bar charts for top referrers, countries, devices, browsers.
   - Data from `AnalyticsService.GetStatsAsync(linkId)`; subscribe to the `link:{linkId:D}` hub group
     for realtime updates.
   - Removes the Chart.js CDN dependency.

### Phase 4 — Home (create link)

10. New `Components/Pages/Home.razx`:
    - **SfTextBox** for the long URL (validation: must be absolute URL).
    - **SfAutoComplete** for the custom slug, wired to `IsValidCustomSlug` validation logic.
    - Create via the existing service logic (reused from `Program.cs`: 50-link free-tier cap,
      PII tokenization, realtime notification) instead of `POST /api/links`.
    - **SfToast** on success showing the created short URL.

### Phase 5 — Privacy Center

11. New `Components/Pages/PrivacyCenter.razx`:
    - Consent status display + opt-in/opt-out controls (cookie `lynq_consent` still written via JS
      interop or a small endpoint).
    - **Export** — decrypted JSON download via `PiiTokenizationService` (reuses `/api/privacy/export`
      logic).
    - **Delete** — SfDialog confirmation, then account/data deletion (reuses `/api/privacy/delete`
      logic).
    - Static policy pages (`Privacy/Policy`, `Cookies`, `Terms`) can be kept as Razor Pages or
      converted to static SSR components (low priority).

### Phase 6 — Cleanup & verification

12. Delete old Razor Pages (`Pages/Index`, `Pages/Dashboard/*`, `Pages/Privacy/*` UI, `Pages/Error`),
    the Chart.js CDN link, the `@microsoft/signalr` CDN script, and dashboard JS in `wwwroot`.
13. Verify the consent banner still works (cookie `lynq_consent` v1.0, Google Fonts loaded only after
    consent) — port the `_Layout` banner logic into `MainLayout.razor` with JS interop.
14. Update `.github/workflows/ci.yml` if it references removed files (build should still pass).
15. Verification checklist:
    - [ ] `dotnet build` clean
    - [ ] Create link: URL validation, slug validation, 50-link cap
    - [ ] Dashboard: grid renders, copy, delete (dialog), navigate to analytics
    - [ ] Realtime: clicking a short link updates dashboard stats without manual refresh
    - [ ] Analytics: charts render with correct data; realtime refresh
    - [ ] Privacy: export JSON, delete account, consent banner behavior
    - [ ] Redirect: `GET /{shortCode}` still tracks clicks (referrer, UA hash, device/browser/country)
    - [ ] No leftover Razor Pages / JS fetch / Chart.js references

---

## 6. Technical Considerations

- **Auth in interactive Server**: components access the current user via `CascadingAuthenticationState`
  + `AuthenticationStateProvider` (or `IHttpContextAccessor` during initial render). Use
  `UserManager<AppUser>.GetUserId()` where needed.
- **Realtime**: keep `DashboardHub` server-side; the Blazor client subscribes with a server-side
  `HubConnection` (no browser JS needed for the hub itself).
- **PII tokenization**: original URLs and referrers are stored tokenized (`tok1:` AES-256-GCM). Only
  decrypt when displaying to the authenticated owner (dashboard grid, analytics).
- **Theme consistency**: Syncfusion `tailwind3-dark` plus CSS overrides for the `brandCyan` /
  `brandIndigo` / `brandEmerald` accents keeps the current look.
- **Syncfusion license**: without a valid `Syncfusion:LicenseKey`, Syncfusion renders a license banner.
  Register a Community License key (free for qualifying companies/individuals) or the paid key.

---

## 7. Out of Scope

- Moving `/api/*` endpoints into Blazor components (kept for compatibility and external tooling).
- Changing the data model, services, or the redirect/click-tracking pipeline.
- Test-project changes unless CI breaks during the conversion.
