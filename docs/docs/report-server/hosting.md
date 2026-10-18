---
title: Hosting
description: Deploy the report server, and run it behind a reverse proxy or under a path prefix.
---

# Hosting

The report server is an ordinary ASP.NET Core app. Host it anywhere that runs .NET 10: Azure App Service, a container platform, IIS or a VM.

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

To serve the reports under a prefix such as `/reports`:

- If the proxy **strips** the prefix, also send `X-Forwarded-Prefix: /reports` and enable the forwarded prefix in `ForwardedHeadersOptions`.
- If the proxy **keeps** the prefix in the forwarded path, call `app.UsePathBase("/reports")` before routing and before `UseBDTestReportServer`.

Include the prefix in your identity provider's redirect URI, for example `https://reports.example.com/reports/signin-oidc`.

### Trust

Only trust forwarded headers from your proxy:

- Limit allowed hosts with `AllowedHosts`, a semicolon-separated list of host names without schemes, ports or paths. Include the backend host if your proxy sends it in the original `Host` header. Avoid `*`.
- Add your proxy's connection IP to `ForwardedHeadersOptions.KnownProxies`. IPv4-mapped IPv6 connections need the mapped address.
- Do not enable blanket forwarding with `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, and do not clear the trusted proxy lists.
- Run forwarded headers middleware before HTTPS redirection and authentication.

See [ASP.NET Core proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0). The [sample server](sample-server.md#reverse-proxy-settings) shows a working configuration.

## Several instances

Each instance has its own memory cache and its own upload rate limit. With several instances:

- use a shared persistent `DataStore`,
- enforce a shared upload rate limit at the gateway as well.
