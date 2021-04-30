using System;
using BDTest.NetCore.Razor.ReportMiddleware.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace BDTest.NetCore.Razor.ReportMiddleware.Helpers
{
    public static class HttpHelper
    {
        public static string GetBaseUrl(this HttpContext httpContext)
        {
            var currentUrl = new Uri(httpContext.Request.GetEncodedUrl());
            var baseUrl = currentUrl.Scheme + Uri.SchemeDelimiter + currentUrl.Host;
            if (!currentUrl.IsDefaultPort)
            {
                baseUrl += $":{currentUrl.Port}";
            }

            return baseUrl;
        }

        public static string GetCurrentPageNumber(this HttpContext httpContext)
        {
            if(!httpContext.Request.Query.TryGetValue("page", out var stringPageNumber))
            {
                return "1";
            }

            if (string.Equals(stringPageNumber, "all", StringComparison.OrdinalIgnoreCase))
            {
                return "all";
            }

            return int.TryParse(stringPageNumber, out var pageNumber) ? pageNumber.ToString() : "1";
        }

        public static string GetUrlForPageNumber(this HttpContext httpContext, int pageNumber)
        {
            var currentUrl = GetCurrentUrl(httpContext);
            return currentUrl.WithQueryParameter("page", pageNumber.ToString()).ToString();
        }

        public static Uri GetCurrentUrl(this HttpContext httpContext)
        {
            return new (httpContext.Request.GetEncodedUrl());
        }
    }
}