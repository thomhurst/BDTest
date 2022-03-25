using BDTest.Output;

namespace BDTest.Helpers;

public class HtmlWriter
{
    private string TestId { get; }

    internal HtmlWriter(string testId)
    {
        TestId = testId;
    }

    public void Custom(string html)
    {
        TestOutputData.WriteCustomHtmlForReport(TestId, html);
    }

    public void Link(string text, string url)
    {
        Custom($"<a href=\"{url}\" target=\"_blank\">{text}</a><br>");
    }
        
    public void Link(Func<string> body, string url)
    {
        Custom($"<a href=\"{url}\" target=\"_blank\">{body()}</a><br>");
    }

    public void Image(string url)
    {
        Custom($"<img src=\"{url}\" loading=\"lazy\"/><br>");
    }

    public void Text(string value)
    {
        Custom($"<span>{value}</span><br>");
    }

    public void LineBreak()
    {
        Custom("<br>");
    }
}