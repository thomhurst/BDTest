using System.Net;
using System.Text;
using BDTest.Contracts;

namespace BDTest.Rendering;

/// <summary>Shared, HTML-encoded structured argument markup for hosted and standalone reports.</summary>
internal static class StepArgumentHtmlHelper
{
    private const int PreviewRows = 20;

    public static string GetHtml(IReadOnlyList<Contracts.StepArgument>? arguments)
    {
        if (arguments == null || arguments.Count == 0) return string.Empty;
        var html = new StringBuilder();
        foreach (var argument in arguments)
        {
            html.Append("<div class=\"step-argument\" style=\"overflow-x:auto\">");
            switch (argument)
            {
                case Contracts.StepTable table:
                    AppendTable(html, table, table.Rows.Take(PreviewRows));
                    if (table.Rows.Count > PreviewRows)
                    {
                        html.Append($"<details><summary>Show all {table.Rows.Count} rows</summary>");
                        AppendTable(html, table, table.Rows.Skip(PreviewRows));
                        html.Append("</details>");
                    }
                    break;
                case Contracts.StepDocString docString:
                    html.Append($"<pre><code data-content-type=\"{Encode(docString.ContentType)}\">{Encode(docString.Content)}</code></pre>");
                    break;
            }
            html.Append("</div>");
        }
        return html.ToString();
    }

    private static void AppendTable(StringBuilder html, Contracts.StepTable table, IEnumerable<IReadOnlyList<string>> rows)
    {
        html.Append("<table class=\"table step-argument-table\"><thead><tr>");
        foreach (var header in table.Headers)
            html.Append($"<th scope=\"col\">{Encode(header)}</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            html.Append("<tr>");
            foreach (var cell in row)
                html.Append($"<td>{Encode(cell)}</td>");
            html.Append("</tr>");
        }
        html.Append("</tbody></table>");
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
