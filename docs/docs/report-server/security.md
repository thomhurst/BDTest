---
title: Securing the report server
description: Protect report pages with authentication, protect uploads with an API key, and control who can delete reports.
---

# Securing the report server

`BDTest.Razor.Reports` supplies controllers and views. It does **not** configure authentication or authorization for your host. Report pages, run lists and `/bdtest/report/{id}/raw-json-data` can contain sensitive test output, such as request bodies, URLs and stack traces. Secure the host before you expose it to other users.

There are three separate controls:

| Control | Protects | Configured by |
| --- | --- | --- |
| Sign-in | Reading reports | Your host's authentication and fallback authorization policy |
| Upload key | `POST /bdtest/data` | `UploadApiKey` option |
| Admin authorizer | Deleting reports | `AdminAuthorizer` option |

## Requiring sign-in

Register an authentication provider and a fallback policy that requires an authenticated user. For example, with Microsoft Entra ID through `Microsoft.Identity.Web` and `Microsoft.Identity.Web.UI`:

```csharp
using BDTest.Razor.Reports.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization(options => options.FallbackPolicy = options.DefaultPolicy);
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI()
    .AddBdTestReportControllersAndViews();
builder.Services.AddRazorPages();
```

The pipeline must select the endpoint before checking its authorization:

```csharp
app.UseBDTestReportServer(options =>
{
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
    options.DataStore = dataStore;
});

app.UseHttpsRedirection();
app.UseStaticFiles(); // Only public assets belong in the web root.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
```

Any ASP.NET Core authentication scheme works. See [Microsoft Identity Web setup](https://github.com/AzureAD/microsoft-identity-web/wiki/web-apps) and [ASP.NET Core authorization policies](https://learn.microsoft.com/aspnet/core/security/authorization/policies).

Every user accepted by your policy can read every report. There are no per-report permissions; restrict who can sign in at the identity provider.

### Anonymous endpoints

- `POST /bdtest/data` is `[AllowAnonymous]` so test jobs do not need an interactive sign-in. It is protected by the upload key instead.
- `GET /bdtest/ping` is anonymous for warm-up checks.
- The library's error page is anonymous.
- Static CSS, JavaScript and images are public.

## Upload key

Uploads require a shared secret in the `X-BDTest-Upload-Key` header:

```csharp
app.UseBDTestReportServer(options =>
{
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
    options.UploadsPerMinute = 60; // Shared by all authenticated clients of this instance.
});
```

- An unset or blank key disables uploads (HTTP 401). Existing reports and the ping endpoint stay available.
- Missing, incorrect or multiple key headers return HTTP 401. Keys are compared in constant time.
- The key and rate limit are checked before the JSON body is parsed.
- A reader's sign-in cookie cannot replace the upload key, and the upload key does not grant read access.
- Configure `UploadApiKey` on every host. No separate upload authentication middleware is needed.

Generate a long random value, keep it out of source control and share it only with the jobs that upload reports. Use HTTPS for both browser sessions and uploads. See [Sending results](sending-results.md) for the client side.

## Administrators

Deleting a report (`POST`/`DELETE /bdtest/report/delete/{id}`) requires `AdminAuthorizer.IsAdminAsync` to return `true`. The default authorizer denies everyone.

```csharp
using BDTest.Razor.Reports.Interfaces;
using Microsoft.AspNetCore.Authorization;

public class AdminAuthorizer : IAdminAuthorizer
{
    public async Task<bool> IsAdminAsync(HttpContext httpContext)
    {
        var authorization = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(httpContext.User, httpContext, "BDTest.Administrator");
        return result.Succeeded;
    }
}

builder.Services.AddAuthorization(options =>
    options.AddPolicy("BDTest.Administrator", policy => policy.RequireRole("BDTest.Admin")));

app.UseBDTestReportServer(options => options.AdminAuthorizer = new AdminAuthorizer());
```

An authorizer must require authentication and check trusted identity claims against a role or policy. Never authorize from the presence or value of a cookie, header or query parameter that the client controls.

## Security headers

`UseBDTestReportServer` adds security headers to every `/bdtest` response, including rejected uploads, redirects and missing reports. Other routes and static assets keep your host's policies.

- A Content Security Policy with a fresh nonce for each report script. It blocks inline event handlers, plugins, base URL changes and embedding reports in other pages, and limits forms and connections to the same origin.
- `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and `X-Frame-Options: DENY`.
- `Cache-Control: no-store`, so HTML and its nonce are not reused.

Inline styles stay allowed for Bootstrap, charts and custom output. Google Fonts is allowed for the theme. Images may use HTTP(S) or `data:` URLs so sandboxed custom HTML can show screenshots. The report CSP is added alongside any CSP your host sets, so both policies must allow a resource.

Hosts with custom views or embedding needs can set `options.EnableSecurityHeaders = false` and send their own compatible headers. A replacement policy must allow the report's scripts; do not simply remove CSP protection. This setting does not change the sandbox on [custom HTML output](../writing-tests/output.md#raw-html).
