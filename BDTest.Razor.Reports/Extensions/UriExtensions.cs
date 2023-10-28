using System.Web;

namespace BDTest.Razor.Reports.Extensions;

public static class UriExtensions
{
    public static Uri WithQueryParameter(this Uri uri, string parameterName, string value)
    {
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
            
        parsedQuery[parameterName] = value;
            
        return new UriBuilder(uri)
        {
            Query = parsedQuery.ToString() ?? string.Empty
        }.Uri;
    }

    public static Uri WithPath(this Uri uri, string path)
    {
        return new UriBuilder(uri)
        {
            Path = path
        }.Uri;
    }
}