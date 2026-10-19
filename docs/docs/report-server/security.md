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

```csharp title="Illustrative fragment"
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
    .AddMicrosoftIdentityUI();
builder.Services.AddBDTestReports(options =>
{
    options.ReportsAuthorizationPolicy = "BDTestReports.Read";
    options.AdminAuthorizationPolicy = "BDTestReports.Admin";
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
});
builder.Services.AddRazorPages();
```

The pipeline must select the endpoint before checking its authorization:

```csharp title="Illustrative fragment"
app.UseBDTestReportServer();

app.UseHttpsRedirection();
app.UseStaticFiles(); // Only public assets belong in the web root.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapBDTestReports().Add(endpoint =>
{
    // Allow test jobs to authenticate with the upload key instead of interactive sign-in.
    var action = endpoint.Metadata
        .OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()
        .SingleOrDefault();
    if (action == null || action.ControllerName == "BDTest" && action.ActionName is "Ping" or "Index" or "Error"
        || action.ControllerName == "Attachments" && action.ActionName == "Upload")
        endpoint.Metadata.Add(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute());
});
app.MapControllers(); // The host's other MVC controllers.
app.MapRazorPages();
```

Any ASP.NET Core authentication scheme works. See [Microsoft Identity Web setup](https://github.com/AzureAD/microsoft-identity-web/wiki/web-apps) and [ASP.NET Core authorization policies](https://learn.microsoft.com/aspnet/core/security/authorization/policies).

A configured read policy takes precedence over `AllowAnonymousReports`. Anonymous failures challenge using the policy's authentication schemes (or the host defaults); authenticated failures forbid access. The host may redirect challenges to sign-in instead of returning 401. Policies may also use custom requirements and handlers; the authorization resource is the current `HttpContext`.

Host fallback policies and endpoint `RequireAuthorization(...)` still apply. `AllowAnonymousReports` only opts out of the library's read check; it does not bypass host authorization. Explicit endpoint authorization also configures read access when no library read policy is set. A fallback policy alone does not count as explicit report-access configuration.

Every user accepted by your policy can read every report. There are no per-report permissions; restrict who can sign in at the identity provider.

### Anonymous endpoints

Report controllers have no built-in `[AllowAnonymous]` exemptions. `.RequireAuthorization("Reports")` on `MapBDTestReports` therefore protects every endpoint, including ping, uploads, errors and assets. A fallback policy applies when you do not supply endpoint authorization metadata.

Ping, error handling and ingestion do not use the library's report-read policy. Host endpoint and fallback authorization still apply to these actions. Report reads continue to fail closed until explicitly configured, and anonymous endpoint conventions never grant delete permission.

The example above explicitly makes upload actions, ping, errors and report assets anonymous, matching the sample server. Upload actions still require the upload key, and attachment writes also require the server's receipt. Omit those exemptions if your jobs authenticate through the host's scheme. Keep reader and download actions protected.

## Upload key

Uploads require a shared secret in the `X-BDTest-Upload-Key` header:

```csharp title="Illustrative fragment"
builder.Services.AddBDTestReports(options =>
{
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
    options.ReportsAuthorizationPolicy = "BDTestReports.Read";
    options.UploadsPerMinute = 60; // Shared by all authenticated clients of this instance.
});
```

- An unset or blank key disables uploads (HTTP 401). This setting does not change reader or ping authorization.
- Missing, incorrect or multiple key headers return HTTP 401. Keys are compared in constant time.
- The key and rate limit are checked before the JSON body is parsed.
- A reader's sign-in cookie cannot replace the upload key, and the upload key does not grant read access.
- Configure `UploadApiKey` on every host. The endpoint must also satisfy your host's authorization policy unless you explicitly allow anonymous ingestion.

Generate a long random value, keep it out of source control and share it only with the jobs that upload reports. Use HTTPS for both browser sessions and uploads. See [Sending results](sending-results.md) for the client side.

## Administrators

Deleting a report (`DELETE /bdtest/reports/{id}`) requires both the read policy (unless anonymous reads are enabled) and the named `AdminAuthorizationPolicy`. Without an admin policy, deletion returns 403, even when anonymous reads are enabled. Deletion also requires an antiforgery token. The run list shows delete buttons only when the current user passes the same admin policy, including its authentication schemes. An unregistered admin policy hides delete buttons without breaking report reads; deletion still returns 403. Admin checks preserve the reader identity so antiforgery tokens remain valid when the admin policy uses a separate sign-in scheme.

Register a policy that requires authentication and trusted role claims, as in the example above. Never authorize from the presence or value of a client-controlled cookie, header or query parameter.

## Migration from anonymous reports and admin authorizers

This major-version change removes `IAdminAuthorizer`, `NoOpAdminAuthorizer` and the `AdminAuthorizer` option. Move custom logic into an ASP.NET Core requirement handler, register a named policy, and set `AdminAuthorizationPolicy` to that policy's name.

Existing private hosts must set `ReportsAuthorizationPolicy` or apply explicit endpoint authorization. A global fallback policy alone does not configure report access. For an intentionally public host, explicitly opt in:

```csharp title="Illustrative fragment"
builder.Services.AddBDTestReports(options =>
{
    options.AllowAnonymousReports = true;
    options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
});
```

This permits report reads only. It grants no upload or delete permission. Alternatively, register an explicit allow-anonymous policy with `RequireAssertion(_ => true)` and use it as `ReportsAuthorizationPolicy`.

Deletion also requires an ASP.NET Core antiforgery request token and its matching cookie. The run list obtains both with `Html.AntiForgeryToken()`. Its delete control sends `fetch` with method `DELETE`, same-origin credentials, and the token in the configured antiforgery header (`RequestVerificationToken` by default). Hosts that disable antiforgery headers use the form field instead. A missing or invalid token returns 400; an admin check that fails returns 401 or 403, depending on authentication state. A host's authentication/authorization middleware may return 401 or 403 before the action runs.

A successful deletion returns 204 with no redirect, even if the report was already absent. IDs use the same rules as bulk reads: 1–128 ASCII letters, digits, dots, hyphens or underscores, excluding `.` and `..`; invalid IDs return 400. Only `DELETE` is accepted at this endpoint; other methods return 405. The old `/bdtest/report/delete/{id}` route is removed. Storage failures propagate as 500 so callers can retry. See [Deletion migration](../migration/report-storage.md#report-deletion).

## Security headers

`UseBDTestReportServer` adds security headers to every response under the prefix selected by `MapBDTestReports`, including assets, rejected uploads, redirects and missing reports. Routes outside that prefix keep your host's policies.

- A Content Security Policy with a fresh nonce for each report script. It blocks inline event handlers, plugins, base URL changes and embedding reports in other pages, and limits forms and connections to the same origin.
- `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and `X-Frame-Options: DENY`.
- `Cache-Control: no-store` on report responses, so HTML and its nonce are not reused. Report assets use a one-day public cache.

Inline styles stay allowed for Bootstrap, charts and custom output. Google Fonts is allowed for the theme. The default image policy is `img-src 'self' data: https:`: same-origin images, `data:` URLs and HTTPS images are allowed. Plain-HTTP third-party images are blocked by default. The report CSP is added alongside any CSP your host sets, so both policies must allow a resource.

### Allowing additional image origins

If `IUserPersonalizer.GetProfilePictureUri` or a custom view uses a plain-HTTP image URL, switch it to HTTPS or explicitly allow its origin:

```csharp title="Illustrative fragment"
builder.Services.AddBDTestReports(options =>
{
    options.AdditionalImageSources = new[] { "http://images.internal.example:8080" };
});
```

`AdditionalImageSources` defaults to an empty list. Assign it before serving requests. Each value must be an absolute HTTP or HTTPS origin, with an optional port and trailing slash. Paths, credentials, query strings, fragments, wildcards, scheme-only sources and CSP expressions are rejected with `ArgumentException`; a null list throws `ArgumentNullException`. Accepted origins are normalized and copied, so later changes to the input list do not change the policy. The option extends only `img-src` and does not remove its defaults.

This is a breaking change for hosts that previously relied on the broad `http:` source. An explicit origin does not override browser mixed-content restrictions on HTTPS pages or a stricter host CSP. Prefer HTTPS image URLs. This option changes the report server's policy only; the separate policies for standalone HTML reports and sandboxed custom HTML remain unchanged.

Hosts with custom views or embedding needs can set `options.EnableSecurityHeaders = false` and send their own compatible headers. A replacement policy must allow the report's scripts; do not simply remove CSP protection. This setting does not change the sandbox on [custom HTML output](../writing-tests/output.md#raw-html).
