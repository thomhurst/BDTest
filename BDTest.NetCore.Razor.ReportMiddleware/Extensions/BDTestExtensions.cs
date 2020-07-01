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
            return new TestRunSummary(bdTestOutputModel.Id, DateTime.Now, bdTestOutputModel.Scenarios.GetTotalStatus(), bdTestOutputModel.Tag, bdTestOutputModel.Environment);
        }

        public static IMvcBuilder AddBdTestReportControllersAndViews(this IMvcBuilder mvcBuilder)
        {
            mvcBuilder
                .AddApplicationPart(typeof(BDTestController).Assembly)
                .AddNewtonsoftJson();

            var services = mvcBuilder.Services;
            
            services.AddMemoryCache();

            services.AddSingleton<IMemoryCacheBdTestDataStore, MemoryCacheBdTestDataStore>();
            services.AddSingleton<IDataController, DataController>();

            return mvcBuilder;
        }
    }
}