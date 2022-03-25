namespace BDTest.NetCore.Razor.ReportMiddleware.Models;

public class CustomLinkData
{
    private readonly Uri _url;

    public CustomLinkData(string text, Uri url)
    {
        Text = text;
        _url = url;
    }

    public string Text { get; }

    public string Url
    {
        get
        {
            if (_url == null)
            {
                return null;
            }

            if (_url.IsAbsoluteUri)
            {
                return _url.AbsoluteUri;
            }

            return _url.ToString();
        }
    }
}