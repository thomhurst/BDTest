---
title: Report options and dependency injection
description: Move report host configuration from middleware to validated options and DI registrations.
---

# Report options and dependency injection

The next major report-server API configures services before the host is built. `BDTestReportServerOptions` and the configure delegate on `UseBDTestReportServer` are removed. Replace them with `BDTestReportOptions` in `BDTest.Razor.Reports.Models` and `AddBDTestReports` in `BDTest.Razor.Reports.Extensions`.

## Register options before building the host

```csharp sample="ServerMigration/Program.cs" title="Compiled example"
using BDTest.Razor.Reports.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBDTestReports(options =>
{
    options.AllowAnonymousReports = true; // Explicitly public reports; use a read policy for private hosts.
    options.ReportCacheRetention = TimeSpan.FromHours(8);
}).BindConfiguration("BDTest");

var app = builder.Build();
app.UseBDTestReportServer();
app.UseRouting();
app.MapBDTestReports();
app.Run();
```

`AddBDTestReports` registers the controllers, views and report services and returns `OptionsBuilder<BDTestReportOptions>`. Configuration binding is optional. In the example, values in the `BDTest` section override the configure delegate. Environment variables use double underscores, for example `BDTest__UploadApiKey` and `BDTest__ReportCacheRetention`.

`ValidateOnStart` is built into registration. Invalid cache budgets, retention, upload limits, rate limits or attachment limits fail host startup with `OptionsValidationException`. A blank upload key remains valid and disables uploads. See [Setup](../report-server/setup.md#options-reference) for values and defaults.

`UseBDTestReportServer()` now only adds the report security-header middleware. Call it after any `UsePathBase` and before authentication, authorization and endpoints. The former configure-delegate overload is removed. `AddBdTestReportControllersAndViews()` remains an obsolete compatibility wrapper; replace it with `builder.Services.AddBDTestReports()`.

Report authorization uses the named `ReportsAuthorizationPolicy` and `AdminAuthorizationPolicy` options. `IAdminAuthorizer` has been removed; move custom checks into ASP.NET Core requirements and handlers. Set `AllowAnonymousReports = true` only for intentionally public reports. See [Security](../report-server/security.md).

## Move extension points to DI

Options contain only configuration values. Replace assignments of service instances with DI registrations before `builder.Build()`:

| Former option | Service to register |
| --- | --- |
| `DataStore` | `IBDTestDataStore` |
| `ReportStore` | `IReportStore` |
| `RunIndexStore` | `IRunIndexStore` |
| `AttachmentStore` | `IBDTestAttachmentStore` |
| `DataReceiver` | `IBDTestDataReceiver` |
| `UserPersonalizer` | `IUserPersonalizer` |
| `CustomSidebarLinksProvider` | `IBDTestCustomSidebarLinksProvider` |
| `CustomHeaderLinksProvider` | `IBDTestCustomHeaderLinksProvider` |
| `Logger` | The host's `ILogger<T>` services |

For example, one implementation can provide both persistent storage contracts without creating two instances:

```csharp title="Illustrative fragment"
builder.Services.AddSingleton<MyReportStore>();
builder.Services.AddSingleton<IReportStore>(services =>
    services.GetRequiredService<MyReportStore>());
builder.Services.AddSingleton<IRunIndexStore>(services =>
    services.GetRequiredService<MyReportStore>());
builder.Services.AddBDTestReports().BindConfiguration("BDTest");
```

`IReportStore` and `IRunIndexStore` are in `BDTest.Razor.Reports.Storage`; other extension interfaces are in `BDTest.Razor.Reports.Interfaces`. Constructors can receive registered dependencies. Singleton implementations must be thread-safe and must not capture scoped dependencies. Register both persistent storage interfaces, or neither for cache-only hosting. An `IBDTestDataStore` registration provides the legacy storage bridge; explicit registrations of both new storage interfaces take precedence.

Custom consumers should inject `IOptions<BDTestReportOptions>` from `Microsoft.Extensions.Options` and read `.Value`. Report services resolve configured options through DI, so repository construction no longer depends on a preceding middleware call. Treat options as startup configuration; do not mutate them while requests are running.

Report JSON, upload routes and storage formats do not change. See [Persistence](../report-server/persistence.md), [Security](../report-server/security.md) and [Customization](../report-server/customization.md) for complete service examples.
