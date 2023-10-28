using BDTest.Maps;
using BDTest.Test;

namespace BDTest.Razor.Reports.Models;

public class ChartViewModel
{
    public int Index { get; set; }
    public List<Scenario> Scenarios { get; set; }
    public List<BDTestOutputModel> Reports { get; set; }
}