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
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
});

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
```

- `AddBdTestReportControllersAndViews()` extends an `IMvcBuilder`. It adds the BDTest controllers and views, a memory cache and the report services. It does not change your MVC formatters or JSON options.
- `UseBDTestReportServer(...)` configures the options and adds security headers to `/bdtest` responses. Call it after `UsePathBase` (if you use it) and before authentication, authorization and endpoints.

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
| `ReportCacheSizeLimitBytes` | `long` | 64 MiB | Budget for cached full reports. Must be positive. |
| `ReportCacheRetention` | `TimeSpan` | 8 hours | How long cached reports are kept after their last use. Must be positive. |
| `EnableSecurityHeaders` | `bool` | `true` | Add CSP and other security headers to `/bdtest` responses. |
| `AdminAuthorizer` | `IAdminAuthorizer` | denies everyone | Decides who may delete reports. See [Security](security.md#administrators). |
| `DataReceiver` | `IBDTestDataReceiver` | `null` | Called after each upload. See [Customization](customization.md#reacting-to-uploads). |
| `CustomHeaderLinksProvider` | `IBDTestCustomHeaderLinksProvider` | `null` | Extra links in the top bar. |
| `CustomSidebarLinksProvider` | `IBDTestCustomSidebarLinksProvider` | `null` | Extra links in the sidebar. |
| `UserPersonalizer` | `IUserPersonalizer` | `null` | User name, picture and links. |
| `Logger` | `ILogger?` | `null` | Logger for report server diagnostics. |

See [Caching and limits](caching-and-limits.md) for the cache options.
