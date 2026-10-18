---
title: Customizing the report server
description: Add team and run links, personalise the page for the signed-in user, and react to uploaded reports.
---

# Customizing the report server

UI extension points are interfaces in `BDTest.Razor.Reports.Interfaces`. Storage interfaces live in `BDTest.Razor.Reports.Storage`; see [Persistence](persistence.md) and [Storage migration](../migration/report-storage.md). Register them with DI before building the host. Implementations can receive their dependencies through constructors. The examples use singletons; ensure singleton implementations are thread-safe and do not capture scoped dependencies. Links use `CustomLinkData(string text, Uri url)`; pass `null` as the URL for plain text.

Configure report read authorization as shown in [Security](security.md#requiring-sign-in) before using these option snippets.

## Header links

Implement `IBDTestCustomHeaderLinksProvider` to add links or text to the **Links** menu in the page header, under **Team links**:

```csharp
public class CustomHeaderLinksProvider : IBDTestCustomHeaderLinksProvider
{
    public IEnumerable<CustomLinkData> GetCustomLinks() => new List<CustomLinkData>
    {
        new("Team wiki", new Uri("https://wiki.example.com/qa")),
        new("Build server", new Uri("https://ci.example.com")),
    };
}

builder.Services.AddSingleton<IBDTestCustomHeaderLinksProvider, CustomHeaderLinksProvider>();
```

## Run links

Implement `IBDTestCustomSidebarLinksProvider` to add links for the current page to the **Links** menu in the page header. On a run's pages they appear under **This run**. Links open in a new tab.

```csharp
public class CustomSidebarLinksProvider : IBDTestCustomSidebarLinksProvider
{
    public IEnumerable<CustomLinkData> GetCustomLinks([AllowNull] BDTestOutputModel currentTestReport)
    {
        // currentTestReport is null on pages that are not about one report.
        if (currentTestReport?.BranchName is { } branch)
        {
            yield return new CustomLinkData($"Commits on {branch}", new Uri($"https://github.com/my-org/my-app/commits/{branch}"));
        }

        yield return new CustomLinkData("Runbook", new Uri("https://wiki.example.com/runbook"));
    }
}

builder.Services.AddSingleton<IBDTestCustomSidebarLinksProvider, CustomSidebarLinksProvider>();
```

Use the current report to build links relevant to it, for example to the build, the branch or a dashboard.

## User personalisation

Implement `IUserPersonalizer` to show the signed-in user's name, picture and personal links. This works with any authentication; the example reads Entra ID claims:

```csharp
public class UserPersonalizer : IUserPersonalizer
{
    public Uri? GetProfilePictureUri(HttpContext httpContext) => null;

    public string? GetNameOfUser(HttpContext httpContext) =>
        httpContext.User.FindFirst("name")?.Value;

    public List<CustomLinkData> GetLinksForUser(HttpContext httpContext) => new()
    {
        new("My account", new Uri("https://myaccount.microsoft.com/")),
    };
}

builder.Services.AddSingleton<IUserPersonalizer, UserPersonalizer>();
```

## Custom delete controls

Generate the delete URL with `Url.ReportAction("DeleteReport", new { id })` (from `BDTest.Razor.Reports.Helpers`) so it includes the host's path base and the prefix selected by `MapBDTestReports`. Send `DELETE` with the antiforgery token and cookie; refresh the run list only after HTTP 204. The built-in control confirms the selection, prevents duplicate submissions, and shows a retry message on failure. JavaScript is required for deletion; a plain POST form is rejected with 405. See [Security](security.md#administrators) for authorization and token requirements, and [Deletion migration](../migration/report-storage.md#report-deletion) for custom clients.

## Reacting to uploads

Implement `IBDTestDataReceiver` to run code each time a report is accepted, for example to notify a chat channel about failures on the main branch:

```csharp
public class FailureNotifier : IBDTestDataReceiver
{
    private readonly HttpClient _httpClient = new();

    public async Task OnReceiveTestDataAsync(BDTestOutputModel report)
    {
        var failures = report.Scenarios.Count(scenario => scenario.Status == Status.Failed);
        if (failures > 0 && report.BranchName == "main")
        {
            await _httpClient.PostAsJsonAsync("https://chat.example.com/webhook",
                new { text = $"{failures} BDTest scenarios failed on main ({report.Environment})" });
        }
    }
}

builder.Services.AddSingleton<IBDTestDataReceiver, FailureNotifier>();
```

The receiver is called after the report is stored, with the server-generated report ID in `report.Id`. It is not called for rejected uploads.

## Dates and localization

Report tables and chart labels use the current request culture's short date pattern. The same date shows as `17/10/2026` in `en-GB`, `10/17/2026` in `en-US` and `17.10.2026` in `de-DE`. Times keep a 24-hour format with up to three fractional second digits, and no time zone conversion happens.

To use each viewer's browser language, configure request localization before routing and the report endpoints:

```csharp
var reportCultures = new[] { "en-GB", "en-US", "de-DE" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-GB")
    .AddSupportedCultures(reportCultures)
    .AddSupportedUICultures(reportCultures));
```

The default providers check the `culture` query parameter, then the culture cookie, then the `Accept-Language` header, and fall back to the default culture. You can also set `DateTimeFormat.ShortDatePattern` on the `CultureInfo` instances you register. Without request localization, the host's current culture decides the format.
