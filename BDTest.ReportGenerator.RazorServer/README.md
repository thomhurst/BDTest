# Sample report server authentication

The sample host uses Microsoft Identity Web to sign users in with Microsoft Entra ID.
Administrator operations require both an authenticated user and the `BDTest.Admin` app role.
Setting a cookie named `admin` does not grant access.

Report reads require sign-in, and uploads require the separate `BDTest:UploadApiKey`.
See the [report host security setup](../README.md#securing-a-report-server) for upload configuration.

Configure the `AzureAd` section for your app registration, alongside the existing
`AzureStorage:ConnectionString` setting:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-application-id>",
    "CallbackPath": "/signin-oidc"
  }
}
```

Register `https://<your-host>/signin-oidc` as a **Web** redirect URI in Entra ID.
Supply `AzureAd:ClientSecret` through user secrets for local development or
`AzureAd__ClientSecret` through deployment secret configuration; do not commit credentials.
Authentication requires valid tenant/application configuration and credentials.

Define an app role with value `BDTest.Admin`, allowed for users/groups, on the app
registration and assign it to administrators through the enterprise application.
This is an application role, not an Entra directory administrator role. Sign in at
`/MicrosoftIdentity/Account/SignIn`; sign out at `/MicrosoftIdentity/Account/SignOut`.
Sign in again after a role assignment changes so the session receives updated claims.
Ordinary signed-in users and anonymous visitors cannot perform administrator operations.

See the [Microsoft Identity Web web app guidance](https://github.com/AzureAD/microsoft-identity-web/wiki/web-apps)
and [Entra app role setup](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps).

## Hosts using the reporting library

Register services before building the host with `builder.Services.AddBDTestReports().BindConfiguration("BDTest")`, then call parameterless `app.UseBDTestReportServer()` before report endpoints. Configure policy names through `BDTestReportOptions` and register storage and UI extension points with DI. See [report options migration](../docs/docs/migration/report-options.md).

Report reads fail closed until `ReportsAuthorizationPolicy` names a registered ASP.NET
Core policy, the host applies explicit endpoint authorization, or
`AllowAnonymousReports = true` explicitly enables public reads. The
sample uses `BDTestReports.Read` for authenticated readers and `BDTestReports.Admin`
for authenticated users with the `BDTest.Admin` role. Set `AdminAuthorizationPolicy`
to the admin policy; an unset value disables deletion. Both policies use standard
ASP.NET Core requirements and handlers. This replaces `IAdminAuthorizer` and the
`AdminAuthorizer` option in the next major version.

Register authentication services and run `UseAuthentication()` and `UseAuthorization()`
after routing and before report endpoints. Never authorize solely from the presence
or value of a client-controlled cookie, header, or query parameter.

## Storage startup

Set `AzureStorage:ConnectionString` using user secrets or the
`AzureStorage__ConnectionString` environment variable. Missing, blank, or malformed
values fail startup with an options validation error naming that setting.

The sample uses the `BDTest.Reports.Storage.AzureBlob` package through
`AddAzureBlobReportStore().BindConfiguration("AzureStorage")`. The package awaits
container creation and index cache warm-up before serving requests. Storage
failures are logged and fail startup; cancellation reaches initialization and
modern index queries. The constructor performs no network requests.

See the [package README](../BDTest.Reports.Storage.AzureBlob/README.md) for token
credentials, custom container names, standalone registration, and migration from
a copied sample implementation. Manual construction requires awaiting
`InitializeAsync` before reading or writing.

## Azure report listing and retention

The modern index scans summary blobs and pages results by stored run start time,
with at most eight concurrent downloads. The host loads all pages during warm-up.
The legacy `GetAllTestRunRecords` API still returns up to 50 summaries in blob
creation order. Existing report and summary JSON remain readable.

Reads and server initialization no longer delete reports or hide them by age.
Configure retention separately on the storage account before upgrading if you
require automatic cleanup. Without a policy, reports remain until explicitly deleted.

[azure-storage-lifecycle.json](azure-storage-lifecycle.json) provides a 30-day Azure
lifecycle rule scoped to both `test-runs-summaries/` and `test-data/`. In the Azure
portal, open the storage account's **Lifecycle management** page and merge this
rule into the existing policy using **Code view**, preserving any other rules.
The server does not install the policy or require management-plane permissions.

The rule expires current blobs 30 days after their last modification and snapshots
and previous versions 30 days after creation. The store writes immutable report
IDs, so modification normally coincides with upload; a manual overwrite restarts
the retention period. Azure applies the rule asynchronously and independently to
each blob, so summary and report deletion is not atomic. Soft-delete and immutable
storage settings can delay physical removal. See Microsoft's
[lifecycle policy documentation](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-policy-structure)
and [configuration instructions](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-policy-configure).

## Report security headers

UseBDTestReportServer enables security headers for /bdtest responses,
including rejected uploads, redirects, and missing reports. Register it after
UsePathBase (when used), and before authentication, authorization, and endpoints.
Other host routes and static asset responses keep the host's policies.

The default Content Security Policy requires a fresh nonce on every report script,
blocks inline event handlers, plugins, base URL changes, and embedding reports in
other pages, and restricts forms and connections to the same origin. Report Razor
views apply the nonce automatically; click handlers use the bundled JavaScript.
X-Content-Type-Options: nosniff, Referrer-Policy: no-referrer, and
X-Frame-Options: DENY are also sent. Report responses use Cache-Control: no-store
so HTML and its nonce are not reused by caches.

Inline styles remain allowed for Bootstrap, charts, and custom output formatting.
The theme's existing Google Fonts stylesheet and font origins are also allowed.
Images may use HTTP(S) or data URLs so sandboxed custom HTML can still display
screenshots; scripts in that custom output remain blocked by its iframe sandbox
and its own CSP. The report CSP is appended to any existing host CSP, so both
policies must permit a resource for it to load.

Hosts with custom views or embedding requirements can set
options.EnableSecurityHeaders = false and provide their own compatible security
headers. A replacement policy must accommodate the report's scripts; do not simply
remove CSP protection. This setting does not change the custom-output iframe
sandbox. See the [CSP specification](https://www.w3.org/TR/CSP/) for nonce and
multiple-policy behavior.
