using System.Collections.Generic;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models.ViewModels
{
    public class TestTimesPieChartViewModel
    {
        public List<Scenario> Scenarios { get; set; }
        public int StoryIndex { get; set; }
    }
}