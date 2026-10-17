# Sample report server authentication

The sample host uses Microsoft Identity Web to sign users in with Microsoft Entra ID.
Administrator operations require both an authenticated user and the `BDTest.Admin` app role.
Setting a cookie named `admin` does not grant access.

Report reads require sign-in, and uploads require the separate `ReportUpload:ApiKey`.
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

`IAdminAuthorizer` is the authorization boundary for privileged report operations.
Custom implementations must require authentication and check trusted identity claims
against an administrator role or policy. Never authorize solely from the presence or
value of a client-controlled cookie, header, or query parameter. Register authentication
and authorization services and run `UseAuthentication()` and `UseAuthorization()` after
routing and before report endpoints. The library's default authorizer denies admin access.

## Azure report listing and retention

The Azure store returns up to 50 summaries, newest blob creation time first (last
modified time is the fallback when creation time is absent). Names break timestamp
ties in ordinal order. This is upload order, not the test run's start time. Existing
GUID blob names remain readable; no rename or migration is required.

Listing scans all metadata pages but keeps only 50 candidates between pages. It
then downloads those summaries with at most eight concurrent requests per listing,
returning them in the selected order. A summary deleted between listing and download
is omitted. Other storage errors still fail the request. Listing cost grows with
the number of stored blobs, so configure retention for long-running installations.

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
