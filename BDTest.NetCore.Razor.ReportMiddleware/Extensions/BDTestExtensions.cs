using System.Linq;
using BDTest.Maps;
using BDTest.NetCore.Razor.ReportMiddleware.Controllers;
using BDTest.NetCore.Razor.ReportMiddleware.Implementations;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using BDTest.NetCore.Razor.ReportMiddleware.Models;
using BDTest.Test;
using Microsoft.Extensions.DependencyInjection;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions
{
    public static class BDTestExtensions
    {
        internal static TestRunSummary GetOverview(this BDTestOutputModel bdTestOutputModel)
        {
            return new TestRunSummary(bdTestOutputModel.Id, 
                bdTestOutputModel.TestTimer.TestsStartedAt,
                bdTestOutputModel.TestTimer.TestsFinishedAt,
                bdTestOutputModel.Scenarios.GetTotalStatus(),
                bdTestOutputModel.Scenarios.Count,
                bdTestOutputModel.Scenarios.Count(scenario => scenario.Status == Status.Failed),
                bdTestOutputModel.Tag, 
                bdTestOutputModel.Environment,
                bdTestOutputModel.Version);
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