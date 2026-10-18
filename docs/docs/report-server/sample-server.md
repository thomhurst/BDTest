---
title: Sample server
description: Run and deploy BDTest.ReportGenerator.RazorServer, the sample report host with Azure Blob Storage and Microsoft Entra ID.
---

# Sample server

[`BDTest.ReportGenerator.RazorServer`](https://github.com/thomhurst/BDTest/tree/main/BDTest.ReportGenerator.RazorServer) is a complete report host in the repository. It is not published as a package: copy it, or use it as a reference for your own host.

It uses:

- `BDTest.Razor.Reports` for the pages,
- Azure Blob Storage for reports (`AzureStorageDataStore`),
- Microsoft Entra ID sign-in through `Microsoft.Identity.Web`,
- an app role for administrators,
- forwarded headers for reverse proxies.

## Configuration

The host calls `AddBDTestReports().BindConfiguration("BDTest")` before building the application. The `BDTest` section binds to `BDTestReportOptions`; invalid option values fail startup. Report storage, attachment storage, administrator authorization, custom links and user personalization are DI services. Parameterless `UseBDTestReportServer()` adds security headers in the request pipeline. See [Setup](setup.md) for the library API and [Report options migration](../migration/report-options.md) for upgrading existing hosts.

| Setting | Environment variable | Description |
| --- | --- | --- |
| `AzureAd:TenantId` | `AzureAd__TenantId` | Your Entra tenant ID. |
| `AzureAd:ClientId` | `AzureAd__ClientId` | The app registration's client ID. |
| `AzureAd:ClientSecret` | `AzureAd__ClientSecret` | The client secret, or configure a certificate through Microsoft.Identity.Web. |
| `AzureStorage:ConnectionString` | `AzureStorage__ConnectionString` | Blob Storage connection string. Required; a missing or malformed value fails startup. |
| `BDTest:UploadApiKey` | `BDTest__UploadApiKey` | The [upload key](security.md#upload-key). Uploads are disabled when empty. |
| `AllowedHosts` | `AllowedHosts` | Semicolon-separated host names. Default: `localhost;127.0.0.1;[::1]`. |
| `ReverseProxy:KnownProxies` | `ReverseProxy__KnownProxies__0`, `__1`, … | IP addresses of trusted proxies. Loopback is always trusted. |

For local development, use user secrets:

```bash
dotnet user-secrets set "AzureStorage:ConnectionString" "<connection string>" --project BDTest.ReportGenerator.RazorServer
dotnet user-secrets set "AzureAd:ClientSecret" "<secret>" --project BDTest.ReportGenerator.RazorServer
dotnet user-secrets set "BDTest:UploadApiKey" "<long random secret>" --project BDTest.ReportGenerator.RazorServer
```

Keep secrets out of committed configuration.

## Entra ID setup

1. Register a single-tenant **web** application.
2. Add `https://<your-report-host>/signin-oidc` as a Web redirect URI. Include any [path prefix](hosting.md#path-prefixes).
3. Create a client secret or certificate.
4. Define an app role with value `BDTest.Admin`, allowed for users and groups. Assign it to administrators through the enterprise application. This is an application role, not a directory administrator role.
5. Restrict who may sign in through the enterprise application's user assignment. By default every user accepted by the application can read every report.

Users sign in at `/MicrosoftIdentity/Account/SignIn` and sign out at `/MicrosoftIdentity/Account/SignOut`. After a role assignment changes, sign in again so the session gets the new claims. Setting a cookie named `admin` grants nothing.

The sample sets `ReportsAuthorizationPolicy = "BDTestReports.Read"` and `AdminAuthorizationPolicy = "BDTestReports.Admin"`. Read access requires sign-in; deletion also requires the `BDTest.Admin` role. Upload API keys remain independent.

## Storage

On startup, the server creates the `test-runs-summaries` and `test-data` containers and warms the cache before serving requests. Storage failures are logged and fail startup, so check connectivity and permissions before restarting.

The store returns up to 50 summaries, newest blob creation time first (last-modified time when creation time is missing). This is upload order, not test start time. It downloads them with at most eight concurrent requests.

The server does not delete old reports. Apply [`azure-storage-lifecycle.json`](https://github.com/thomhurst/BDTest/blob/main/BDTest.ReportGenerator.RazorServer/azure-storage-lifecycle.json) to the storage account: open **Lifecycle management**, switch to **Code view** and merge the rule into the existing policy. It deletes blobs in both containers 30 days after their last modification, and snapshots and previous versions 30 days after creation. Azure applies the rule asynchronously per blob, and soft-delete or immutability settings can delay removal.

## Reverse proxy settings

The sample accepts only `localhost`, `127.0.0.1` and `[::1]` by default. For a deployment:

- Set `AllowedHosts` to your host names, for example `reports.example.com;report-backend.internal`. The same list constrains `X-Forwarded-Host`.
- For a remote proxy, set its connection IP through `ReverseProxy__KnownProxies__0`.
- The default limit is one forwarding hop. More complex chains need explicit `ForwardedHeadersOptions` in `Program.CreateBuilder`.

See [Hosting](hosting.md#behind-a-reverse-proxy) for the general rules.

## Request pipeline

`Program.BuildApplication` runs, in order: forwarded headers, the exception handler and HSTS (outside Development), HTTPS redirection, static files with one-day caching, `UseBDTestReportServer`, routing, authentication and authorization, then the controller and Razor page endpoints.
