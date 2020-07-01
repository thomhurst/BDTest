using System;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class CustomLinkData
    {
        public CustomLinkData(string text, Uri url)
        {
            Text = text;
            Url = url;
        }

        public Uri Url { get; }
        public string Text { get; }
    }
}