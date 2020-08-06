using System;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Controllers;
using BDTest.NetCore.Razor.ReportMiddleware.Implementations;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
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
            return AddBdTestReportControllersAndViews(mvcBuilder, options => { });
        }

        public static IMvcBuilder AddBdTestReportControllersAndViews(this IMvcBuilder mvcBuilder, Action<BDTestReportServerOptions> options)
        {
            mvcBuilder
                .AddApplicationPart(typeof(BDTestController).Assembly)
                .AddNewtonsoftJson();

            var services = mvcBuilder.Services;
            
            services.AddMemoryCache();
            
            var reportServerOptionsModel = new BDTestReportServerOptions();
            options(reportServerOptionsModel);
            services.AddSingleton(reportServerOptionsModel);
            
            services.AddSingleton<IMemoryCacheBdTestDataStore, MemoryCacheBdTestDataStore>();
            services.AddSingleton<IDataController, DataController>();

            return mvcBuilder;
        }
    }
}