using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

public class TestTimesPieChartViewModel
{
    public string ReportId { get; set; }
    public List<Scenario> Scenarios { get; set; }
    public int StoryIndex { get; set; }
}