using System;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer.Models;

namespace BDTest.ReportGenerator.RazorServer.Extensions
{
    public static class BDTestExtensions
    {
        public static TestRunOverview GetOverview(this BDTestOutputModel bdTestOutputModel)
        {
            return new TestRunOverview(bdTestOutputModel.Id, DateTime.Now, bdTestOutputModel.Scenarios.GetTotalStatus(), bdTestOutputModel.Tag, bdTestOutputModel.Environment);
        }
    }
}