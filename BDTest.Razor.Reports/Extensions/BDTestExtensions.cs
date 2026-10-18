using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Security;
using BDTest.Razor.Reports.Routing;
using BDTest.Razor.Reports.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BDTest.Razor.Reports.Extensions;

public static class BDTestExtensions
{
    internal static TestRunSummary GetOverview(this BDTestOutputModel bdTestOutputModel)
    {
        return new TestRunSummary(bdTestOutputModel);
    }

    /// <summary>Registers report controllers, views, services, and startup-validated options.</summary>
    public static OptionsBuilder<BDTestReportOptions> AddBDTestReports(this IServiceCollection services,
        Action<BDTestReportOptions> configure = null)
    {
        services.AddControllersWithViews().AddApplicationPart(typeof(BDTestController).Assembly);
        services.TryAddSingleton<ReportRoutes>();
        services.AddOptions<Microsoft.AspNetCore.Mvc.MvcOptions>().Configure<ReportRoutes>((options, routes) =>
        {
            if (!options.Conventions.Contains(routes)) options.Conventions.Add(routes);
        });
        services.AddMemoryCache();
        services.TryAddSingleton<IMemoryCacheBdTestDataStore>(provider =>
        {
            // Resolve storage outside its constructor to avoid a circular dependency.
            var dataStore = provider.GetService<IBDTestDataStore>();
            var reportStore = provider.GetService<IReportStore>();
            // One memory entry must own retention and attachment cleanup. A second cache
            // could retain a report after its backing memory store deletes the attachments.
            if ((reportStore ?? dataStore as IReportStore) is IMemoryCacheBdTestDataStore memoryStore)
                return memoryStore;
            return new MemoryCacheBdTestDataStore(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IOptions<BDTestReportOptions>>(),
                provider.GetRequiredService<ReportAttachmentGate>(),
                provider.GetService<IBDTestAttachmentStore>(),
                provider.GetService<ILogger<MemoryCacheBdTestDataStore>>(),
                hasPersistentStorage: dataStore != null || reportStore != null);
        });
        services.TryAddSingleton<IDataRepository, DataRepository>();
        services.TryAddSingleton<ReportUploadFilter>();
        services.TryAddSingleton<ReportAttachmentGate>();
        services.AddAuthorization();
        var options = services.AddOptions<BDTestReportOptions>();
        if (configure != null) options.Configure(configure);
        return options
            .Validate(value => value.ReportCacheSizeLimitBytes > 0, "ReportCacheSizeLimitBytes must be positive.")
            .Validate(value => value.ReportCacheRetention > TimeSpan.Zero, "ReportCacheRetention must be positive.")
            .Validate(value => value.MaxUploadBytes > 0, "MaxUploadBytes must be positive.")
            .Validate(value => value.MaxCompressedUploadBytes > 0, "MaxCompressedUploadBytes must be positive.")
            .Validate(value => value.UploadsPerMinute > 0, "UploadsPerMinute must be positive.")
            .Validate(value => value.MaximumAttachmentBytes >= 0 && value.MaximumReportAttachmentBytes >= 0
                && value.MaximumAttachmentsPerReport >= 0, "Attachment limits must be nonnegative.")
            .ValidateOnStart();
    }

    [Obsolete("Use services.AddBDTestReports() before building the application.")]
    public static IMvcBuilder AddBdTestReportControllersAndViews(this IMvcBuilder mvcBuilder)
    {
        mvcBuilder.Services.AddBDTestReports();
        return mvcBuilder;
    }

    /// <summary>Maps report controllers and assets under a literal prefix. Call once before mapping other MVC controllers.</summary>
    public static IEndpointConventionBuilder MapBDTestReports(this IEndpointRouteBuilder endpoints, string prefix = "/bdtest")
    {
        var routes = endpoints.ServiceProvider.GetRequiredService<ReportRoutes>();
        routes.Map(prefix);
        var assets = endpoints.MapGroup(routes.Prefix + "/assets");
        MapAsset(assets, "/css/bdtest-report.css", "text/css");
        MapAsset(assets, "/js/BDTest.js", "text/javascript");
        MapAsset(assets, "/favicon.ico", "image/x-icon");
        return new ReportRoutes.Conventions(ReportRoutes.MapControllers(endpoints), assets);
    }

    private static void MapAsset(RouteGroupBuilder assets, string path, string contentType)
    {
        assets.MapGet(path, (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "public,max-age=86400";
            var stream = typeof(BDTestExtensions).Assembly.GetManifestResourceStream("BDTest.Razor.Reports.wwwroot" + path.Replace('/', '.'));
            return Results.Stream(stream, contentType);
        }).WithName("BDTest.Asset" + path).WithMetadata(new ReportRoutes.AssetMetadata());
    }

    /// <summary>Adds report response security headers. Configure options through AddBDTestReports before Build.</summary>
    public static void UseBDTestReportServer(this IApplicationBuilder applicationBuilder)
    {
        var options = applicationBuilder.ApplicationServices.GetRequiredService<IOptions<BDTestReportOptions>>().Value;
        var routes = applicationBuilder.ApplicationServices.GetRequiredService<ReportRoutes>();
        applicationBuilder.Use(async (context, next) =>
        {
            if (options.EnableSecurityHeaders && routes.Prefix != null && context.Request.Path.StartsWithSegments(routes.Prefix))
            {
                // Register before MVC so redirects, rejected requests and errors also receive headers.
                context.Response.OnStarting(() =>
                {
                    ReportSecurityHeaders.Apply(context, preventCaching: context.GetEndpoint()?.Metadata.GetMetadata<ReportRoutes.AssetMetadata>() == null);
                    return Task.CompletedTask;
                });
            }
            await next();
        });
    }
}
