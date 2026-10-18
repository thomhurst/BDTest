using BDTest.Razor.Reports.Constants;
using System.Web;
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
        return httpContext.GetLocalUrlWithQueryParameter(PagerQueryParameters.Page, pageNumber.ToString());
    }

    // Keep navigation independent of the untrusted Host header, including behind a proxy.
    public static string GetLocalUrl(this HttpContext httpContext, string? path = null)
    {
        var request = httpContext.Request;
        var localPath = request.PathBase.Add(path == null ? request.Path : new PathString(path)).ToUriComponent();
        // A leading double slash is a network-path reference in browsers.
        return "/" + localPath.TrimStart('/') + request.QueryString.ToUriComponent();
    }

    public static string GetLocalUrlWithQueryParameter(this HttpContext httpContext, string name, string? value, string? path = null)
    {
        var query = HttpUtility.ParseQueryString(httpContext.Request.QueryString.Value ?? string.Empty);
        query[name] = value;
        var localUrl = httpContext.GetLocalUrl(path);
        var queryIndex = localUrl.IndexOf('?');
        return (queryIndex < 0 ? localUrl : localUrl.Substring(0, queryIndex)) + "?" + query;
    }

    public static Uri GetCurrentUrl(this HttpContext httpContext)
    {
        return new Uri(httpContext.Request.GetEncodedUrl());
    }

    public static string? GetQueryParameter(this HttpRequest httpRequest, string queryParameterName)
    {
        if (httpRequest.Query.TryGetValue(queryParameterName, out var queryParameterValue))
        {
            return queryParameterValue;
        }
            
        return null;
    }
}