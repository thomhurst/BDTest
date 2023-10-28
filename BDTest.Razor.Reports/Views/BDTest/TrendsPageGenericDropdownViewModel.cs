namespace BDTest.Razor.Reports.Views.BDTest;

public class TrendsPageGenericDropdownViewModel
{
    public string QueryParameterName { get; set; }
    public string ButtonText { get; set; }
    public string ButtonId { get; set; }
    public List<string> DropdownValues { get; set; } = new();
}