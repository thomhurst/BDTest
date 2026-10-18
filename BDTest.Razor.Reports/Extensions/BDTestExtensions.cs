using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddMemoryCache();
        services.TryAddSingleton<IMemoryCacheBdTestDataStore, MemoryCacheBdTestDataStore>();
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

    /// <summary>Adds report response security headers. Configure options through AddBDTestReports before Build.</summary>
    public static void UseBDTestReportServer(this IApplicationBuilder applicationBuilder)
    {
        var options = applicationBuilder.ApplicationServices.GetRequiredService<IOptions<BDTestReportOptions>>().Value;
        applicationBuilder.Use(async (context, next) =>
        {
            if (options.EnableSecurityHeaders && context.Request.Path.StartsWithSegments("/bdtest"))
            {
                // Register before MVC so redirects, rejected requests and errors also receive headers.
                context.Response.OnStarting(() =>
                {
                    ReportSecurityHeaders.Apply(context);
                    return Task.CompletedTask;
                });
            }
            await next();
        });
    }
}
