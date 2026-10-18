using System.Security.Claims;
using BDTest.ReportGenerator.RazorServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class SampleAdminPolicyTests
{
    [TestCase(null, null, null, false)]
    [TestCase(null, null, "admin=true", false)]
    [TestCase(null, "BDTest.Admin", null, false)]
    [TestCase("Cookies", null, null, false)]
    [TestCase("Cookies", "Reader", "admin=true", false)]
    [TestCase("Cookies", "BDTest.Admin", null, true)]
    public async Task Only_authenticated_administrators_are_authorized(
        string authenticationType, string role, string cookie, bool expected)
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var identity = new ClaimsIdentity(authenticationType);
        if (role != null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = new ClaimsPrincipal(identity)
        };
        if (cookie != null)
        {
            context.Request.Headers.Cookie = cookie;
        }

        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        Assert.That((await authorization.AuthorizeAsync(context.User, context, "BDTestReports.Admin")).Succeeded, Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Authentication_middleware_accepts_only_a_protected_admin_session(bool tampered)
    {
        await using var app = CreateApp();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var signInContext = new DefaultHttpContext { RequestServices = services };
        signInContext.Request.Scheme = "https";
        signInContext.Request.Host = new HostString("reports.example.test");
        await signInContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, "BDTest.Admin")],
                CookieAuthenticationDefaults.AuthenticationScheme)));
        var cookie = signInContext.Response.Headers.SetCookie.Single().Split(';')[0];
        if (tampered)
        {
            cookie = cookie[..(cookie.IndexOf('=') + 1)] + "forged-admin-ticket";
        }

        // Use a separate request scope so handlers cannot reuse the sign-in request's state.
        await using var requestScope = app.Services.CreateAsyncScope();
        var request = new DefaultHttpContext { RequestServices = requestScope.ServiceProvider };
        request.Request.Scheme = "https";
        request.Request.Host = signInContext.Request.Host;
        request.Request.Path = "/bdtest/report/test-runs";
        request.Request.Headers.Cookie = cookie + "; admin=true";
        // Exercise authentication and the admin policy even for an invalid session.
        // The host fallback policy is covered by its HTTP integration tests.
        request.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()), null));
        var pipeline = new ApplicationBuilder(app.Services);
        pipeline.UseAuthentication();
        pipeline.UseAuthorization();
        pipeline.Run(async context =>
        {
            var authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
            Assert.That((await authorization.AuthorizeAsync(context.User, context, "BDTestReports.Admin")).Succeeded, Is.EqualTo(!tampered));
        });

        await pipeline.Build()(request);
    }

    private static WebApplication CreateApp()
    {
        var builder = Program.CreateBuilder([]);
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["AzureStorage:ConnectionString"] = "UseDevelopmentStorage=true",
            ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
            ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222"
        });
        return builder.Build();
    }
}
