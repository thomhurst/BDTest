using System;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Controllers;
using BDTest.NetCore.Razor.ReportMiddleware.Implementations;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions
{
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
}