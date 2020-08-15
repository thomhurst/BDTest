using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace BDTest.NetCore.Razor.ReportMiddleware.Helpers
{
    public static class HttpHelper
    {
        public static string GetBaseUrl(HttpContext httpContext)
        {
            var currentUrl = new Uri(httpContext.Request.GetEncodedUrl());
            var baseUrl = currentUrl.Scheme + Uri.SchemeDelimiter + currentUrl.Host;
            if (!currentUrl.IsDefaultPort)
            {
                baseUrl += $":{currentUrl.Port}";
            }

            return baseUrl;
        }
    }
}