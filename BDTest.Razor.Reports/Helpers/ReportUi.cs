using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using Microsoft.AspNetCore.Html;

namespace BDTest.Razor.Reports.Helpers;

/// <summary>Shared presentation helpers for the report views.</summary>
public static class ReportUi
{
    /// <summary>Statuses in the order the report lists them: problems first, then passes.</summary>
    public static readonly Status[] DisplayOrder =
    {
        Status.Failed, Status.NotImplemented, Status.Inconclusive, Status.Skipped,
        Status.NotRun, Status.Pending, Status.Passed
    };

    /// <summary>
    /// The result shown for a whole run. Not implemented and skipped scenarios are deliberate, so such runs count as
    /// passed; inconclusive, pending and unstarted runs keep their own status and never count as passed.
    /// </summary>
    public static Status RunResult(this Status runStatus) =>
        runStatus is Status.NotImplemented or Status.Skipped ? Status.Passed : runStatus;

    /// <summary>Incomplete scenario counts take precedence over intentional skips and unimplemented steps.</summary>
    public static Status RunResult(this TestRunSummary run)
    {
        if (run.Counts?.Failed > 0 || run.Status == Status.Failed)
            return Status.Failed;
        if (run.Counts?.Inconclusive > 0 || run.Status == Status.Inconclusive)
            return Status.Inconclusive;
        if (run.Counts?.NotRun > 0 || run.Status == Status.NotRun)
            return Status.NotRun;
        if (run.Counts?.Pending > 0 || run.Status == Status.Pending)
            return Status.Pending;
        return run.Status.RunResult();
    }

    public static string Key(this Status status) => status switch
    {
        Status.Passed => "p",
        Status.Failed => "f",
        Status.NotImplemented => "n",
        Status.Inconclusive => "i",
        Status.Skipped => "s",
        _ => "r"
    };

    public static string IconId(this Status status) => status switch
    {
        Status.Passed => "i-pass",
        Status.Failed => "i-fail",
        Status.NotImplemented => "i-ni",
        Status.Inconclusive => "i-inc",
        Status.Skipped => "i-skip",
        Status.Pending => "i-clock",
        _ => "i-notrun"
    };

    public static string Label(this Status status) => status switch
    {
        Status.NotImplemented => "Not implemented",
        Status.NotRun => "Not run",
        _ => status.ToString()
    };

    public static IHtmlContent Icon(string id, string? cssClass = null) =>
        new HtmlString($"<svg class=\"ico{(string.IsNullOrEmpty(cssClass) ? "" : " " + cssClass)}\" aria-hidden=\"true\"><use href=\"#{id}\"></use></svg>");

    public static IHtmlContent StatusIcon(this Status status, string? cssClass = null) =>
        Icon(status.IconId(), "ico-" + status.Key() + (string.IsNullOrEmpty(cssClass) ? "" : " " + cssClass));

    public static IHtmlContent Pill(this Status status, string? text = null) =>
        new HtmlString($"<span class=\"pill {status.Key()}\"><svg class=\"ico\" aria-hidden=\"true\"><use href=\"#{status.IconId()}\"></use></svg>{HtmlEncoder.Default.Encode(text ?? status.Label())}</span>");

    /// <summary>Formats a duration the way people read it: "0.21 s", "2.4 s", "1m 42s" or "1h 3m".</summary>
    public static string ToShortDuration(this TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return "0 s";
        }

        if (span.TotalSeconds < 1)
        {
            return span.TotalSeconds.ToString("0.00", CultureInfo.CurrentCulture) + " s";
        }

        if (span.TotalSeconds < 60)
        {
            return span.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture) + " s";
        }

        if (span.TotalHours < 1)
        {
            return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        }

        return $"{(int)span.TotalHours}h {span.Minutes}m";
    }

    /// <summary>"18 Oct 2026, 02:00" in the request culture, or an empty string when unset.</summary>
    public static string ToRunDate(this DateTime dateTime, bool includeYear = true, bool withSeconds = false)
    {
        if (dateTime == default)
        {
            return string.Empty;
        }

        var format = (includeYear ? "d MMM yyyy, " : "d MMM, ") + (withSeconds ? "HH:mm:ss" : "HH:mm");
        return dateTime.ToString(format, CultureInfo.CurrentCulture);
    }

    public static string ToDay(this DateTime dateTime) =>
        dateTime == default ? string.Empty : dateTime.ToString("d MMM", CultureInfo.CurrentCulture);

    public static string ToClock(this DateTime dateTime, bool milliseconds = false) =>
        dateTime == default ? string.Empty : dateTime.ToString(milliseconds ? "HH:mm:ss.fff" : "HH:mm:ss", CultureInfo.CurrentCulture);

    public static string Percent(double part, double total) =>
        total <= 0 ? "0" : (part / total * 100).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Splits "Given a thing" into the keyword and the rest so the keyword can be muted.</summary>
    public static (string Keyword, string Text) SplitStepText(string? stepText)
    {
        if (string.IsNullOrEmpty(stepText))
        {
            return (string.Empty, string.Empty);
        }

        var space = stepText.IndexOf(' ');
        var first = space < 0 ? stepText : stepText[..space];
        return first is "Given" or "When" or "Then" or "And" or "But"
            ? (first, space < 0 ? string.Empty : stepText[(space + 1)..])
            : (string.Empty, stepText);
    }

    /// <summary>The non-empty lines of a story, for example "As a customer", "I want to buy items".</summary>
    public static IReadOnlyList<string> StoryLines(string? story) =>
        IsMissingStory(story)
            ? Array.Empty<string>()
            : story.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();

    public static bool IsMissingStory([NotNullWhen(false)] string? story) =>
        string.IsNullOrWhiteSpace(story) || story == LegacyReportDefaults.StoryTextNotDefined;

    /// <summary>A short name for a story. "I want to buy items" becomes "Buy items"; other stories use their first line.</summary>
    public static string StoryTitle(string? story)
    {
        var lines = StoryLines(story);
        if (lines.Count == 0)
        {
            return "No story";
        }

        var want = lines.FirstOrDefault(line => line.StartsWith("I want ", StringComparison.OrdinalIgnoreCase));
        if (want == null)
        {
            return lines[0];
        }

        var title = want["I want ".Length..].Trim();
        if (title.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
        {
            title = title[3..];
        }

        return title.Length == 0 ? lines[0] : char.ToUpper(title[0], CultureInfo.CurrentCulture) + title[1..];
    }

    /// <summary>The story as one sentence: "As a customer, I want to buy items, so that I receive my order".</summary>
    public static string StorySentence(string? story)
    {
        var lines = StoryLines(story);
        return string.Join(", ", lines.Select((line, index) =>
            index > 0 && line.StartsWith("So that ", StringComparison.Ordinal) ? "so" + line[2..] : line));
    }

    /// <summary>Initials for the account button when no picture is available.</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length == 1 ? parts[0][..1] : parts[0][..1] + parts[^1][..1];
        return initials.ToUpperInvariant();
    }
}
