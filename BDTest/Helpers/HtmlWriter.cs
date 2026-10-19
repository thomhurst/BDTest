using System.Net;
using BDTest.Output;

namespace BDTest.Helpers;

public class HtmlWriter
{
    private readonly BDTestRun _run;
    private string TestId { get; }

    internal HtmlWriter(string testId, BDTestRun run)
    {
        TestId = testId;
        _run = run ?? throw new ArgumentNullException(nameof(run));
    }

    /// <summary>Writes raw HTML without encoding. Use only for intentional markup.</summary>
    public void Custom(string? html)
    {
        TestOutputData.WriteCustomHtmlForReport(TestId, html, _run);
    }

    /// <summary>Writes encoded text linked to an HTTP(S), mailto, or relative URL.</summary>
    /// <exception cref="ArgumentException">The URL violates the allowed URL policy.</exception>
    public void Link(string? text, string url)
    {
        Custom($"<a href=\"{EncodeUrl(url, allowMailto: true)}\" target=\"_blank\">{WebUtility.HtmlEncode(text)}</a><br>");
    }

    /// <summary>Evaluates the delegate once and writes its result as encoded link text.</summary>
    /// <exception cref="ArgumentException">The URL violates the allowed URL policy.</exception>
    public void Link(Func<string?> body, string url)
    {
        Custom($"<a href=\"{EncodeUrl(url, allowMailto: true)}\" target=\"_blank\">{WebUtility.HtmlEncode(body())}</a><br>");
    }

    /// <summary>Writes an image with an encoded HTTP(S) or relative URL.</summary>
    /// <exception cref="ArgumentException">The URL violates the allowed URL policy.</exception>
    public void Image(string url)
    {
        Custom($"<img src=\"{EncodeUrl(url, allowMailto: false)}\" loading=\"lazy\"/><br>");
    }

    public void Text(string? value)
    {
        Custom($"<span>{WebUtility.HtmlEncode(value)}</span><br>");
    }

    public void LineBreak()
    {
        Custom("<br>");
    }

    private static string EncodeUrl(string url, bool allowMailto)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (url.Length == 0 || char.IsWhiteSpace(url[0]) || char.IsWhiteSpace(url[url.Length - 1])
            || url.Any(character => char.IsControl(character) || character == '\\'))
        {
            throw new ArgumentException("URLs must be nonempty and contain no control characters, backslashes, or surrounding whitespace.", nameof(url));
        }

        // Check the original spelling, not System.Uri's platform-dependent interpretation of paths.
        // Browsers strip tabs/newlines before parsing schemes, so reject controls above first.
        for (var index = 0; index < url.Length; index++)
        {
            var character = url[index];
            if (character == '/' || character == '?' || character == '#')
            {
                break; // Relative path, query, or fragment; later colons cannot introduce a scheme.
            }

            if (character != ':')
            {
                continue;
            }

            var scheme = url.Substring(0, index);
            if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                && !scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                && !(allowMailto && scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Only HTTP(S) and relative URLs are allowed, plus mailto URLs for links.", nameof(url));
            }
            break;
        }

        return WebUtility.HtmlEncode(url);
    }
}
