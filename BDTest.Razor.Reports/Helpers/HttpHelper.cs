using BDTest.Razor.Reports.Constants;
using BDTest.Razor.Reports.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace BDTest.Razor.Reports.Helpers;

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
        if(!httpContext.Request.Query.TryGetValue(PagerQueryParameters.Page, out var stringPageNumber))
        {
            return "1";
        }

        if (string.Equals(stringPageNumber, PagerQueryParameters.All, StringComparison.OrdinalIgnoreCase))
        {
            return PagerQueryParameters.All;
        }

        return int.TryParse(stringPageNumber, out var pageNumber) ? pageNumber.ToString() : "1";
    }

    public static string GetUrlForPageNumber(this HttpContext httpContext, int pageNumber)
    {
        var currentUrl = GetCurrentUrl(httpContext);
        return currentUrl.WithQueryParameter(PagerQueryParameters.Page, pageNumber.ToString()).ToString();
    }

    public static Uri GetCurrentUrl(this HttpContext httpContext)
    {
        return new Uri(httpContext.Request.GetEncodedUrl());
    }

    public static string GetQueryParameter(this HttpRequest httpRequest, string queryParameterName)
    {
        if (httpRequest.Query.TryGetValue(queryParameterName, out var queryParameterValue))
        {
            return queryParameterValue;
        }
            
        return null;
    }
}