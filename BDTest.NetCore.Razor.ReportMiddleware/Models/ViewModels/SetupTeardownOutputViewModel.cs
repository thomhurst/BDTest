namespace BDTest.NetCore.Razor.ReportMiddleware.Models.ViewModels;

public class SetupTeardownOutputViewModel
{
    public SetupTeardownOutputViewModel(string header, string content)
    {
        Header = header;
        Content = content;
    }

    public string Header { get; }
    public string Content { get; }
}