---
title: Setting up a report server
description: Register BDTest report services, bind validated options, and add the report middleware.
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

builder.Services.AddBDTestReports(options => options.AllowAnonymousReports = true)
    .BindConfiguration("BDTest");

var app = builder.Build();

app.UseBDTestReportServer();

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
```

- `AddBDTestReports()` extends `IServiceCollection`. It adds the BDTest controllers and views, a memory cache and the report services. It does not change your MVC formatters or JSON options.
- Registration returns `OptionsBuilder<BDTestReportOptions>`, so you can chain `BindConfiguration("BDTest")`. An optional configure delegate supports settings in code. Validation runs on host startup.
- `UseBDTestReportServer()` adds security headers to `/bdtest` responses. It does not configure services or options. Call it after `UsePathBase` (if you use it) and before authentication, authorization and endpoints.

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

`BDTestReportOptions` (namespace `BDTest.Razor.Reports.Models`) contains configuration values only. Configure it during service registration, before building the host:

```csharp
builder.Services.AddBDTestReports(options =>
{
    options.ReportCacheSizeLimitBytes = 128L * 1024 * 1024;
    options.ReportCacheRetention = TimeSpan.FromHours(12);
}).BindConfiguration("BDTest");
```

In this example, bound configuration overrides values from the delegate. For example, `appsettings.json` can contain:

```json
{
  "BDTest": {
    "ReportCacheSizeLimitBytes": 134217728,
    "ReportCacheRetention": "12:00:00",
    "UploadsPerMinute": 60
  }
}
```

Supply `BDTest:UploadApiKey` through user secrets or an environment variable. Options are validated on startup: cache and upload byte limits and retention must be positive, `UploadsPerMinute` must be positive, and attachment limits must be nonnegative. A blank upload key is allowed and disables uploads. Invalid values fail startup with `OptionsValidationException`.

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `UploadApiKey` | `string?` | `null` | Shared secret required in the `X-BDTest-Upload-Key` header. Uploads are disabled when unset. |
| `UploadsPerMinute` | `int` | `60` | Upload rate limit per server instance. |
| `MaxCompressedUploadBytes` | `long` | 10 MiB | Positive request body byte limit before decompression, also for plain requests. |
| `MaxUploadBytes` | `long` | 10 MiB | Positive decompressed JSON and UTF-8 byte limit; also bounds historical reference expansion. |
| `ReportCacheSizeLimitBytes` | `long` | 64 MiB | Budget for cached full reports. Must be positive. |
| `ReportCacheRetention` | `TimeSpan` | 8 hours | How long cached reports are kept after their last use. Must be positive. |
| `EnableSecurityHeaders` | `bool` | `true` | Add CSP and other security headers to `/bdtest` responses. |
| `MaximumAttachmentBytes` | `long` | 10 MiB | Maximum bytes per attachment. Must be nonnegative. |
| `MaximumReportAttachmentBytes` | `long` | 100 MiB | Maximum attachment bytes per report. Must be nonnegative. |
| `MaximumAttachmentsPerReport` | `int` | `1000` | Maximum attachments per report. Must be nonnegative. |
| `ReportsAuthorizationPolicy` | `string?` | `null` | Named policy for all report reads. Unset denies reads unless endpoint authorization or anonymous access is enabled. |
| `AllowAnonymousReports` | `bool` | `false` | Explicitly permit anonymous reads when no read policy is configured. Host authorization still applies. |
| `AdminAuthorizationPolicy` | `string?` | `null` | Named policy for deleting reports. Unset disables deletion. See [Security](security.md#administrators). |

Register storage and UI extension points as DI services. Use `IReportStore` together with `IRunIndexStore` for [persistent storage](persistence.md), `IBDTestAttachmentStore` for attachments, named ASP.NET Core policies for [administrator access](security.md#administrators), and the interfaces in [Customization](customization.md) for links, personalization and upload callbacks. They can receive constructor dependencies. Diagnostics use the host's `ILogger<T>` services.

See [Caching and limits](caching-and-limits.md) for the cache options and [Report options migration](../migration/report-options.md) when upgrading an existing host.
