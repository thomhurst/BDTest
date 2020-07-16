using System.Collections.Generic;
using BDTest.Maps;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models.ViewModels
{
    public class SummaryViewModel
    {
        public List<Scenario> Scenarios { get; set; }
        public BDTestOutputModel TotalReportData { get; set; }
    }
}