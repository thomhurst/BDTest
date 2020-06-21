using System.Collections.Generic;
using BDTest.Test;

namespace BDTest.ReportGenerator.RazorServer.Models
{
    public class TestTimesPieChartViewModel
    {
        public List<Scenario> Scenarios { get; set; }
        public int StoryIndex { get; set; }
    }
}