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
