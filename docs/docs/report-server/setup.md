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
Razor views. Embedded assets are served under the prefix selected by `MapBDTestReports`,
by default `/bdtest/assets/`. You do not need to copy the views or assets into your app.

`TomLonghurst.EnumerableAsyncProcessor` is no longer a dependency of the report
package. If your app uses that package, add a direct package reference to it.

## 2. Register the services

```csharp sample="Server/Program.cs" title="Compiled example"
using BDTest.Razor.Reports.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBDTestReports(options => options.AllowAnonymousReports = true)
    .BindConfiguration("BDTest");

var app = builder.Build();

app.UseBDTestReportServer();

app.UseRouting();
app.MapBDTestReports();

app.Run();
```

- `AddBDTestReports()` extends `IServiceCollection`. It adds the BDTest controllers and views, a memory cache and the report services. It does not change your MVC formatters or JSON options.
- Registration returns `OptionsBuilder<BDTestReportOptions>`, so you can chain `BindConfiguration("BDTest")`. An optional configure delegate supports settings in code. Validation runs on host startup.
- `UseBDTestReportServer()` adds security headers to responses under the mapped report prefix. It does not configure services or options. Call it after `UsePathBase` (if you use it) and before authentication, authorization and endpoints.

## Configure file-system storage

Install `BDTest.Reports.Storage.FileSystem` to keep report bytes and run metadata
in a directory. Add `using BDTest.Reports.Storage.FileSystem;`, then register it
alongside `AddBDTestReports` before building the host:

```csharp title="Illustrative fragment"
builder.Services.AddBDTestFileSystemStorage()
    .BindConfiguration("BDTestFileSystem");
```

```json title="appsettings.json"
{
  "BDTestFileSystem": {
    "DirectoryPath": "/var/lib/my-reports",
    "Retention": "30.00:00:00"
  }
}
```

Use an absolute path suitable for your OS (for example `C:\\ReportData` in JSON
on Windows), outside the web root. Environment variables can supply
`BDTestFileSystem__DirectoryPath` and `BDTestFileSystem__Retention`.
Startup validation requires a fully qualified path and retention greater than
zero and at most 36500 days. The path has no default; retention defaults to 30 days.
The store snapshots these options when created; restart the host to change them.
The directory must be writable and accessible without symbolic links or reparse
points. See [Persistence](persistence.md#file-system-package) for retention,
concurrency and filesystem requirements.

## Choose the report prefix

`app.MapBDTestReports(string prefix = "/bdtest")` maps the report controllers and their CSS, JavaScript and favicon. Call it once, before `MapControllers`, `MapControllerRoute`, or resolving MVC action descriptors. `MapControllers` alone no longer exposes reports. `MapBDTestReports` publishes only report endpoints; call `MapControllers` or `MapControllerRoute` separately to expose the host's controllers. Report conventions do not apply to those host endpoints. The prefix must be a non-root literal path containing letters, digits, slashes, hyphens or underscores; a trailing slash is accepted.

```csharp title="Illustrative fragment"
app.MapBDTestReports("/team/reports")
    .RequireAuthorization("Reports");
```

The returned `IEndpointConventionBuilder` applies authorization, rate limiting, CORS and other endpoint conventions to every report endpoint, including uploads and assets. It does not apply them to your host's controllers. Register the corresponding ASP.NET Core services and middleware in your host. See [Security](security.md#anonymous-endpoints) for explicitly allowing upload jobs through an interactive sign-in policy.

The prefix itself redirects to `<prefix>/report/test-runs`. The library leaves `/` to your host. If your application needs a home-page redirect, map it explicitly. Report assets are served at `<prefix>/assets/...`; `UseStaticFiles` is not needed for them. Remove old links to `/_content/BDTest.Razor.Reports/...` from custom layouts.

Custom Razor views can use `Url.ReportAction("Summary", new { id = report.Id })` for report pages and `Url.ReportScenario(report.Id, scenario.Guid)` for scenario details. `ReportScenario` returns `null` when either ID is missing, so Razor omits the `href` while preserving the scenario text. This also supports older stored reports that lack scenario IDs.

For custom prefixes, upload clients pass the full report base address, such as `https://reports.example.com/team/reports`. See [Hosting](hosting.md#path-prefixes) for proxies and `PathBase`.

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

```csharp title="Illustrative fragment"
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
| `EnableSecurityHeaders` | `bool` | `true` | Add CSP and other security headers under the mapped report prefix. |
| `MaximumAttachmentBytes` | `long` | 10 MiB | Maximum bytes per attachment. Must be nonnegative. |
| `MaximumReportAttachmentBytes` | `long` | 100 MiB | Maximum attachment bytes per report. Must be nonnegative. |
| `MaximumAttachmentsPerReport` | `int` | `1000` | Maximum attachments per report. Must be nonnegative. |
| `AdditionalImageSources` | `IReadOnlyList<string>` | Empty | Extra HTTP(S) image origins, copied and validated on assignment. See [Security](security.md#allowing-additional-image-origins). |
| `ReportsAuthorizationPolicy` | `string?` | `null` | Named policy for all report reads. Unset denies reads unless endpoint authorization or anonymous access is enabled. |
| `AllowAnonymousReports` | `bool` | `false` | Explicitly permit anonymous reads when no read policy is configured. Host authorization still applies. |
| `AdminAuthorizationPolicy` | `string?` | `null` | Named policy for deleting reports. Unset disables deletion. See [Security](security.md#administrators). |

Register storage and UI extension points as DI services. Use `IReportStore` together with `IRunIndexStore` for [persistent storage](persistence.md), `IBDTestAttachmentStore` for attachments, named ASP.NET Core policies for [administrator access](security.md#administrators), and the interfaces in [Customization](customization.md) for links, personalization and upload callbacks. They can receive constructor dependencies. Diagnostics use the host's `ILogger<T>` services.

See [Caching and limits](caching-and-limits.md) for the cache options and [Report options migration](../migration/report-options.md) when upgrading an existing host.
