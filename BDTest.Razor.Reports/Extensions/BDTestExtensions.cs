using BDTest.Maps;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BDTest.Razor.Reports.Extensions;

public static class BDTestExtensions
{
    internal static TestRunSummary GetOverview(this BDTestOutputModel bdTestOutputModel)
    {
        return new TestRunSummary(bdTestOutputModel);
    }

    public static IMvcBuilder AddBdTestReportControllersAndViews(this IMvcBuilder mvcBuilder)
    {
        mvcBuilder
            .AddApplicationPart(typeof(BDTestController).Assembly)
            .AddNewtonsoftJson();

        var services = mvcBuilder.Services;
            
        services.AddMemoryCache();

        services.AddSingleton<IMemoryCacheBdTestDataStore, MemoryCacheBdTestDataStore>();
        services.AddSingleton<IDataRepository, DataRepository>();
        services.AddSingleton(new BDTestReportServerOptions());

        return mvcBuilder;
    }

    public static void UseBDTestReportServer(this IApplicationBuilder applicationBuilder, Action<BDTestReportServerOptions> options)
    {
        var reportServerOptionsModel = applicationBuilder.ApplicationServices.GetService<BDTestReportServerOptions>();
            
        options(reportServerOptionsModel);
    }
}