namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class CustomLinkData
    {
        public CustomLinkData(string text, string url)
        {
            Text = text;
            Url = url;
        }

        public string Url { get; }
        public string Text { get; }
    }
}