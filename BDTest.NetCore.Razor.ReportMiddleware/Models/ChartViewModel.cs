using System.Collections.Generic;
using BDTest.Maps;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class ChartViewModel
    {
        public int Index { get; set; }
        public List<Scenario> Scenarios { get; set; }
        public List<BDTestOutputModel> Reports { get; set; }
    }
}