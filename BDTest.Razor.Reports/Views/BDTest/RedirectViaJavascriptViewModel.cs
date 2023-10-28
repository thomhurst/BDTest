namespace BDTest.Razor.Reports.Views.BDTest;

public class RedirectViaJavascriptViewModel
{
    public RedirectViaJavascriptViewModel(string fromPath, string toPath)
    {
        FromPath = fromPath;
        ToPath = toPath;
    }

    public string FromPath { get; set; }
    public string ToPath { get; set; }
}