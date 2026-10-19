using System.Net;
using System.Text;
using BDTest.Contracts;

namespace BDTest.Helpers;

/// <summary>Shared, HTML-encoded exception trees for hosted and standalone reports.</summary>
public static class ExceptionHtmlHelper
{
    // Compatibility entry point for the hosted report model until its migration.
    public static string GetHtml(Test.ExceptionWrapper? exception) => GetHtml(ResultSnapshot.Create(exception));

    internal static IReadOnlyList<ExceptionInfo> GetRootCauses(this ExceptionInfo exception)
    {
        var causes = new List<ExceptionInfo>();
        var pending = new Stack<ExceptionInfo>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current.InnerExceptions.Count == 0) causes.Add(current);
            else for (var index = current.InnerExceptions.Count - 1; index >= 0; index--)
                pending.Push(current.InnerExceptions[index]);
        }
        return causes;
    }

    public static string GetHtml(ExceptionInfo? exception) => GetHtml(exception,
        (html, text) => html.Append($"<pre>{Encode(text)}</pre>"));

    internal static string GetHtml(ExceptionInfo? exception, Action<StringBuilder, string?> appendOriginalText)
    {
        if (exception == null) return string.Empty;
        var html = new StringBuilder();
        AppendException(html, exception, 0, appendOriginalText);
        return html.ToString();
    }

    private static void AppendException(StringBuilder html, ExceptionInfo exception, int depth,
        Action<StringBuilder, string?> appendOriginalText)
    {
        html.Append(depth == 0 ? "<details class=\"exception-tree\" open>" : "<details class=\"exception-tree\">");
        html.Append($"<summary><strong>{Encode(exception.TypeName)}</strong>: {Encode(exception.Message)}");
        if (exception.IsWrapper) html.Append(" <span class=\"exception-wrapper\">(wrapper)</span>");
        html.Append("</summary><div class=\"exception-details\" style=\"margin-left:1rem;overflow-x:auto\">");
        if (!string.IsNullOrEmpty(exception.StackTrace)) html.Append($"<pre>{Encode(exception.StackTrace)}</pre>");
        if (exception.HResult.HasValue) html.Append($"<p>HResult: {exception.HResult.Value}</p>");
        if (!string.IsNullOrEmpty(exception.Source)) html.Append($"<p>Source: {Encode(exception.Source)}</p>");
        if (exception.Data?.Count > 0)
        {
            html.Append("<dl>");
            foreach (var entry in exception.Data) html.Append($"<dt>{Encode(entry.Key)}</dt><dd>{Encode(entry.Value)}</dd>");
            html.Append("</dl>");
        }
        if (!string.IsNullOrEmpty(exception.TruncationReason))
            html.Append($"<p>Exception traversal stopped: {Encode(exception.TruncationReason)}</p>");
        if (exception.InnerExceptions?.Count > 0)
        {
            // Bound rendering too: reports can come from third-party producers or be modified after capture.
            if (depth >= 9) html.Append("<p>Exception traversal stopped: DepthLimit</p>");
            else
            {
                foreach (var child in exception.InnerExceptions)
                    if (child != null) AppendException(html, child, depth + 1, appendOriginalText);
            }
        }
        if (exception.AsString != null && !string.IsNullOrEmpty(exception.AsString))
        {
            html.Append("<details><summary>Original exception text</summary>");
            appendOriginalText(html, exception.AsString);
            html.Append("</details>");
        }
        html.Append("</div></details>");
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
