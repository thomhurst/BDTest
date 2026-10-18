---
title: Securing the report server
description: Protect report pages with authentication, protect uploads with an API key, and control who can delete reports.
---

# Securing the report server

Report pages, run lists, raw JSON, attachments and bulk comparisons can contain sensitive test output. The library denies these reads with HTTP 403 until you configure `ReportsAuthorizationPolicy`, apply explicit endpoint authorization with `RequireAuthorization(...)`, or explicitly set `AllowAnonymousReports = true`. Each configuration denial logs a warning naming the missing option through ASP.NET Core logging. Missing named policies also fail closed and log an error.

The library registers authorization services, but your host supplies authentication schemes and policies. This applies even when the host has no fallback policy or forgets to call `UseBDTestReportServer`.

There are three separate controls:

| Control | Protects | Configured by |
| --- | --- | --- |
| Sign-in | Reading reports | `ReportsAuthorizationPolicy` and host authentication |
| Upload key | `POST /bdtest/data` | `UploadApiKey` option |
| Admin policy | Deleting reports | `AdminAuthorizationPolicy` option |

## Requiring sign-in

Register an authentication provider and a named read policy that requires an authenticated user. For example, with Microsoft Entra ID through `Microsoft.Identity.Web` and `Microsoft.Identity.Web.UI`:

```csharp
using BDTest.Razor.Reports.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BDTestReports.Read", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("BDTestReports.Admin", policy =>
        policy.RequireAuthenticatedUser().RequireRole("BDTest.Admin"));
});
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
    options.ReportsAuthorizationPolicy = "BDTestReports.Read";
    options.AdminAuthorizationPolicy = "BDTestReports.Admin";
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

A configured read policy takes precedence over `AllowAnonymousReports`. Anonymous failures challenge using the policy's authentication schemes (or the host defaults); authenticated failures forbid access. The host may redirect challenges to sign-in instead of returning 401. Policies may also use custom requirements and handlers; the authorization resource is the current `HttpContext`.

Host fallback policies and endpoint `RequireAuthorization(...)` still apply. `AllowAnonymousReports` only opts out of the library's read check; it does not bypass host authorization. Explicit endpoint authorization also configures read access when no library read policy is set. A fallback policy alone does not count as explicit report-access configuration.

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
    options.ReportsAuthorizationPolicy = "BDTestReports.Read";
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

Deleting a report (`POST`/`DELETE /bdtest/report/delete/{id}`) requires both the read policy (unless anonymous reads are enabled) and the named `AdminAuthorizationPolicy`. Without an admin policy, deletion returns 403, even when anonymous reads are enabled. Both methods also require an antiforgery token. The run list shows delete buttons only when the current user passes the same admin policy, including its authentication schemes. An unregistered admin policy hides delete buttons without breaking report reads; deletion still returns 403. Admin checks preserve the reader identity so antiforgery tokens remain valid when the admin policy uses a separate sign-in scheme.

Register a policy that requires authentication and trusted role claims, as in the example above. Never authorize from the presence or value of a client-controlled cookie, header or query parameter.

## Migration from anonymous reports and admin authorizers

This major-version change removes `IAdminAuthorizer`, `NoOpAdminAuthorizer` and the `AdminAuthorizer` option. Move custom logic into an ASP.NET Core requirement handler, register a named policy, and set `AdminAuthorizationPolicy` to that policy's name.

Existing private hosts must set `ReportsAuthorizationPolicy` or apply explicit endpoint authorization. A global fallback policy alone does not configure report access. For an intentionally public host, explicitly opt in:

```csharp
app.UseBDTestReportServer(options =>
{
    options.AllowAnonymousReports = true;
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
});
```

This permits report reads only. It grants no upload or delete permission. Alternatively, register an explicit allow-anonymous policy with `RequireAssertion(_ => true)` and use it as `ReportsAuthorizationPolicy`.

## Security headers

`UseBDTestReportServer` adds security headers to every `/bdtest` response, including rejected uploads, redirects and missing reports. Other routes and static assets keep your host's policies.

- A Content Security Policy with a fresh nonce for each report script. It blocks inline event handlers, plugins, base URL changes and embedding reports in other pages, and limits forms and connections to the same origin.
- `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and `X-Frame-Options: DENY`.
- `Cache-Control: no-store`, so HTML and its nonce are not reused.

Inline styles stay allowed for Bootstrap, charts and custom output. Google Fonts is allowed for the theme. Images may use HTTP(S) or `data:` URLs so sandboxed custom HTML can show screenshots. The report CSP is added alongside any CSP your host sets, so both policies must allow a resource.

Hosts with custom views or embedding needs can set `options.EnableSecurityHeaders = false` and send their own compatible headers. A replacement policy must allow the report's scripts; do not simply remove CSP protection. This setting does not change the sandbox on [custom HTML output](../writing-tests/output.md#raw-html).
