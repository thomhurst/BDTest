using BDTest.Maps;
using BDTest.Test;

namespace BDTest.Razor.Reports.Models.ViewModels;

public class SummaryViewModel
{
    public List<Scenario> Scenarios { get; set; }
    public BDTestOutputModel TotalReportData { get; set; }
}