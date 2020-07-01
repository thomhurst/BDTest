namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class CustomTabData
    {
        public CustomTabData(string text, string url)
        {
            Text = text;
            Url = url;
        }

        public string Url { get; }
        public string Text { get; }
    }
}