---
title: Setting up a report server
description: Add BDTest.Razor.Reports to an ASP.NET Core app and configure it with UseBDTestReportServer.
---

# Setting up a report server

## 1. Create an ASP.NET Core app

```bash
dotnet new web -n MyCompany.TestReports
cd MyCompany.TestReports
dotnet add package BDTest.Razor.Reports
```

`BDTest.Razor.Reports` targets `net10.0`. Use the same version as the BDTest packages in your test projects.

The package uses the shared `Microsoft.AspNetCore.App` framework and includes compiled
Razor views. Static web assets are served under `/_content/BDTest.Razor.Reports/`.
You do not need to copy the views or assets into your app. Keep static file middleware
enabled as shown below.

`TomLonghurst.EnumerableAsyncProcessor` is no longer a dependency of the report
package. If your app uses that package, add a direct package reference to it.

## 2. Register the services

```csharp title="Program.cs"
using BDTest.Razor.Reports.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews()
    .AddBdTestReportControllersAndViews();

var app = builder.Build();

app.UseBDTestReportServer(options =>
{
    options.AllowAnonymousReports = true; // Deliberately public local example; use a read policy for private reports.
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
});

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
```

- `AddBdTestReportControllersAndViews()` extends an `IMvcBuilder`. It adds the BDTest controllers and views, a memory cache and the report services. It does not change your MVC formatters or JSON options.
- `UseBDTestReportServer(...)` configures the options and adds security headers to `/bdtest` responses. Call it after `UsePathBase` (if you use it) and before authentication, authorization and endpoints.

The example explicitly permits anonymous report reads. Without this opt-in, a named read policy or explicit endpoint authorization, reports return HTTP 403 and log the missing configuration. For private reports, replace the opt-in with the authentication and policy configuration in [Security](security.md#requiring-sign-in).

## 3. Set an upload key

Uploads are rejected until you configure `UploadApiKey`. Generate a long random secret and keep it out of source control:

```bash
dotnet user-secrets init
dotnet user-secrets set "BDTest:UploadApiKey" "<a long random secret>"
```

In production, use an environment variable (`BDTest__UploadApiKey`) or a secret store.

## 4. Run it

```bash
dotnet run
```

Open `/bdtest/report/test-runs`. You should see an empty list. Next, [send results from your tests](sending-results.md).

:::warning In-memory only
Without persistent stores, reports are kept only in a bounded memory cache. A restart, eviction or expiry loses them. Add [persistent storage](persistence.md) before relying on the server.
:::

## Options reference

`BDTestReportServerOptions` (namespace `BDTest.Razor.Reports.Models`) is configured inside `UseBDTestReportServer`:

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `ReportStore` | `IReportStore` | `null` | Persistent UTF-8 report bytes. Set with `RunIndexStore`. |
| `RunIndexStore` | `IRunIndexStore` | `null` | Paged run metadata, independent of view models. |
| `DataStore` | `IBDTestDataStore` | `null` | Legacy storage bridge. See [Persistence](persistence.md). |
| `UploadApiKey` | `string?` | `null` | Shared secret required in the `X-BDTest-Upload-Key` header. Uploads are disabled when unset. |
| `UploadsPerMinute` | `int` | `60` | Upload rate limit per server instance. |
| `MaxCompressedUploadBytes` | `long` | 10 MiB | Positive request body byte limit before decompression, also for plain requests. |
| `MaxUploadBytes` | `long` | 10 MiB | Positive decompressed JSON and UTF-8 byte limit; also bounds historical reference expansion. |
| `ReportCacheSizeLimitBytes` | `long` | 64 MiB | Budget for cached full reports. Must be positive. |
| `ReportCacheRetention` | `TimeSpan` | 8 hours | How long cached reports are kept after their last use. Must be positive. |
| `EnableSecurityHeaders` | `bool` | `true` | Add CSP and other security headers to `/bdtest` responses. |
| `ReportsAuthorizationPolicy` | `string?` | `null` | Named policy for all report reads. Unset denies reads unless endpoint authorization or anonymous access is enabled. |
| `AllowAnonymousReports` | `bool` | `false` | Explicitly permit anonymous reads when no read policy is configured. Host authorization still applies. |
| `AdminAuthorizationPolicy` | `string?` | `null` | Named policy for deleting reports. Unset disables deletion. See [Security](security.md#administrators). |
| `DataReceiver` | `IBDTestDataReceiver` | `null` | Called after each upload. See [Customization](customization.md#reacting-to-uploads). |
| `CustomHeaderLinksProvider` | `IBDTestCustomHeaderLinksProvider` | `null` | Extra links in the top bar. |
| `CustomSidebarLinksProvider` | `IBDTestCustomSidebarLinksProvider` | `null` | Extra links for the current run, in the header **Links** menu. |
| `UserPersonalizer` | `IUserPersonalizer` | `null` | User name, picture and links. |
| `Logger` | `ILogger?` | `null` | Logger for report server diagnostics. |

See [Caching and limits](caching-and-limits.md) for the cache options.
