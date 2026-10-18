using System.Web;
using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Helpers;

public static class QueryUrl
{
    /// <summary>
    /// The current local URL with some query values changed. A null value removes the parameter.
    /// An optional path replaces the current path (it is combined with the path base).
    /// </summary>
    public static string GetLocalUrlWith(this HttpContext httpContext, string path, params (string Name, string Value)[] changes)
    {
        var query = HttpUtility.ParseQueryString(httpContext.Request.QueryString.Value ?? string.Empty);
        foreach (var (name, value) in changes)
        {
            if (value == null)
            {
                query.Remove(name);
            }
            else
            {
                query[name] = value;
            }
        }

        var localUrl = httpContext.GetLocalUrl(path);
        var queryIndex = localUrl.IndexOf('?');
        var basePath = queryIndex < 0 ? localUrl : localUrl[..queryIndex];
        var queryString = query.ToString();
        return string.IsNullOrEmpty(queryString) ? basePath : basePath + "?" + queryString;
    }

    public static string GetLocalUrlWith(this HttpContext httpContext, params (string Name, string Value)[] changes) =>
        httpContext.GetLocalUrlWith(null, changes);
}
