---
title: Hosting
description: Deploy the report server, and run it behind a reverse proxy or under a path prefix.
---

# Hosting

The report server is an ordinary ASP.NET Core app. Host it anywhere that runs .NET 10: Azure App Service, a container platform, IIS or a VM.

`BDTest.Razor.Reports` targets .NET 10 and depends on `BDTest.Contracts`. A host
does not need the `BDTest` execution package or a test adapter. The built-in and
Azure Blob readers accept schema version 1 and historical unversioned reports.
See [report contract migration](../migration/immutable-results.md#hosted-reports).

For example, on Azure App Service:

1. Create an App Service for .NET 10. The free tier is enough to try it out.
2. Configure the app settings: `BDTest__UploadApiKey`, your storage connection string and your authentication settings. See [Security](security.md).
3. Publish with `dotnet publish -c Release` and deploy the output, or right-click the project in Visual Studio and choose **Publish**.

Before exposing the server:

- Configure [persistent storage](persistence.md). An in-memory server loses reports on restart.
- Require [sign-in](security.md#requiring-sign-in) and set an [upload key](security.md#upload-key).
- Use HTTPS.

## Behind a reverse proxy

Report navigation uses local URLs and preserves `Request.PathBase`. The **copy link** buttons resolve those URLs against the browser's origin. Hosts that embed the Razor library must configure their own host filtering and forwarded headers middleware.

Configure the proxy to overwrite client-supplied forwarding headers and to send `X-Forwarded-For`, `X-Forwarded-Proto` and `X-Forwarded-Host`.

### Path prefixes

Mount reports directly with `app.MapBDTestReports("/reports")`. Navigation, assets, redirects and uploads use that prefix, and the host's `/` remains available.

A reverse proxy can add another path base such as `/team`:

- If the proxy **strips** `/team`, send `X-Forwarded-Prefix: /team` and enable forwarded prefixes in `ForwardedHeadersOptions`.
- If the proxy **keeps** `/team`, call `app.UsePathBase("/team")` before routing and before `UseBDTestReportServer`.

Include the path base in your identity provider's redirect URI, for example `https://reports.example.com/team/signin-oidc`.

Upload clients pass the full external report base address: `https://reports.example.com/team/reports` in this example. The uploader appends `data` and attachment upload paths to that address. The static API also appends `ping` for its warm-up request. Trailing slashes are optional. A bare origin such as `https://reports.example.com` keeps the default `/bdtest` mapping; any non-root path replaces that default, so include `/bdtest` yourself when using `UsePathBase` with the default mapping (for example `https://reports.example.com/team/bdtest`).

### Trust

Only trust forwarded headers from your proxy:

- Limit allowed hosts with `AllowedHosts`, a semicolon-separated list of host names without schemes, ports or paths. Include the backend host if your proxy sends it in the original `Host` header. Avoid `*`.
- Add your proxy's connection IP to `ForwardedHeadersOptions.KnownProxies`. IPv4-mapped IPv6 connections need the mapped address.
- Do not enable blanket forwarding with `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, and do not clear the trusted proxy lists.
- Run forwarded headers middleware before HTTPS redirection and authentication.

See [ASP.NET Core proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0). The [sample server](sample-server.md#reverse-proxy-settings) shows a working configuration.

## Several instances

Each instance has its own memory cache and its own upload rate limit. With several instances:

- register shared persistent `IReportStore` and `IRunIndexStore` services,
- enforce a shared upload rate limit at the gateway as well.
