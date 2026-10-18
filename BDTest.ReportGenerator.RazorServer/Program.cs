using System.Net;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Net.Http.Headers;

namespace BDTest.ReportGenerator.RazorServer;

public class Program
{
    public static void Main(string[] args)
    {
        BuildApplication(CreateBuilder(args)).Run();
    }

    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });
        builder.Configuration.AddUserSecrets<Program>();
        builder.WebHost.UseStaticWebAssets();
        var services = builder.Services;

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedPrefix;
            // Retain the framework's loopback trust and one-hop limit. Never trust arbitrary senders.
            foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var host in (builder.Configuration["AllowedHosts"] ?? "localhost").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                options.AllowedHosts.Add(host);
            }
        });

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = options.DefaultPolicy;
            options.AddPolicy("BDTestReports.Read", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("BDTestReports.Admin", policy =>
                policy.RequireAuthenticatedUser().RequireRole("BDTest.Admin"));
        });

        services
            .AddControllersWithViews()
            .AddMicrosoftIdentityUI()
            .AddBdTestReportControllersAndViews();

        services.AddSingleton<IBDTestDataStore, AzureStorageDataStore>();
        services.AddSingleton<CustomSidebarLinkProvider>();
        services.AddSingleton<UserPersonalizer>();

        AddConfig(services, builder.Configuration);
        services.AddHostedService<ReportStorageStartupService>();

        services.AddRazorPages();

        return builder;
    }

    public static WebApplication BuildApplication(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/bdtest/error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                const int durationInSeconds = 60 * 60 * 24;
                ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                    "public,max-age=" + durationInSeconds;
            }
        });

        app.UseBDTestReportServer(options =>
        {
            options.UploadApiKey = builder.Configuration["BDTest:UploadApiKey"];
            options.DataStore = app.Services.GetRequiredService<IBDTestDataStore>();
            options.AttachmentStore = options.DataStore as IBDTestAttachmentStore;
            options.CustomSidebarLinksProvider = app.Services.GetRequiredService<CustomSidebarLinkProvider>();
            options.ReportsAuthorizationPolicy = "BDTestReports.Read";
            options.AdminAuthorizationPolicy = "BDTestReports.Admin";
            options.UserPersonalizer = app.Services.GetRequiredService<UserPersonalizer>();
        });

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();

        return app;
    }

    private static void AddConfig(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AzureStorageConfig>()
            .Bind(configuration.GetSection("AzureStorage"))
            .Validate(config => config.HasValidConnectionString(),
                "AzureStorage:ConnectionString must be a valid Azure Blob Storage connection string.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<AzureStorageConfig>>().Value);
    }
}
