using BDTest.Razor.Reports.Helpers;
using System.Globalization;
using BDTest.Test;
using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Models;

/// <summary>Filters for lists of test runs, read from and written back to the query string.</summary>
public class RunFilter
{
    public const string AnyTime = "all";
    public const string Last7Days = "7d";
    public const string Last30Days = "30d";
    public const string Today = "today";
    public const string ThisMonth = "month";

    /// <summary>The custom start and end given by <c>datetimerange</c>.</summary>
    public const string Custom = "custom";

    private static readonly string[] Ranges = { Last30Days, Last7Days, Today, ThisMonth, AnyTime };

    public string? Search { get; set; }
    public string? Tag { get; set; }
    public string? Environment { get; set; }
    public string? Branch { get; set; }
    public string? Machine { get; set; }

    /// <summary>"Passed" or "Failed", or empty for any status.</summary>
    public string? Status { get; set; }

    /// <summary>One of the range constants.</summary>
    public string Range { get; set; } = AnyTime;

    /// <summary>A custom start and end, from the older <c>datetimerange=start..end</c> parameter.</summary>
    public (DateTime Start, DateTime End)? CustomRange { get; set; }

    public static RunFilter FromQuery(IQueryCollection query, string defaultRange = AnyTime)
    {
        string? Read(string name, string? placeholder = null)
        {
            var value = query.TryGetValue(name, out var values) ? values.ToString() : null;
            // Older report server links used "All Tags" and similar as the no-filter value.
            return string.IsNullOrWhiteSpace(value) || string.Equals(value, placeholder, StringComparison.Ordinal) ? null : value;
        }

        var filter = new RunFilter
        {
            Search = Read("q")?.Trim(),
            Tag = Read("tag", LegacyReportDefaults.AllTags),
            Environment = Read("environment", LegacyReportDefaults.AllEnvironments),
            Branch = Read("branch", LegacyReportDefaults.AllBranches),
            Machine = Read("machine", LegacyReportDefaults.AllMachines),
            Status = Read("status") is { } status && (status == nameof(Contracts.Status.Passed) || status == nameof(Contracts.Status.Failed)) ? status : null,
            Range = Read("range") is { } range && Ranges.Contains(range) ? range : defaultRange
        };

        // datetimerange=start..end comes from older links, or from the "custom" range kept by the filter forms.
        var rangeValue = Read("range");
        var legacy = Read("datetimerange", LegacyReportDefaults.AllDateTimes);
        if (legacy != null && (rangeValue == null || rangeValue == Custom) && legacy.Contains(".."))
        {
            var parts = legacy.Split("..");
            if (DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start)
                && DateTime.TryParse(parts.ElementAtOrDefault(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var end))
            {
                filter.CustomRange = (start, end);
                filter.Range = AnyTime;
            }
        }

        return filter;
    }

    public IEnumerable<TestRunSummary> Apply(IEnumerable<TestRunSummary> records, DateTime? now = null)
    {
        var (from, to) = GetDates(now ?? DateTime.UtcNow);
        return records.Where(record => record != null
            && Matches(Tag, record.Tag)
            && Matches(Environment, record.Environment)
            && Matches(Branch, record.BranchName)
            && Matches(Machine, record.MachineName)
            && (Status == null || record.RunResult().ToString() == Status)
            && (from == null || record.StartedAtDateTime >= from)
            && (to == null || record.StartedAtDateTime <= to)
            && (Search == null || MatchesSearch(record)));
    }

    public (DateTime? From, DateTime? To) GetDates(DateTime now)
    {
        if (CustomRange is { } custom)
        {
            return (custom.Start, custom.End);
        }

        return Range switch
        {
            Last7Days => (now.AddDays(-7), null),
            Last30Days => (now.AddDays(-30), null),
            Today => (now.Date, null),
            ThisMonth => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, now.Kind), null),
            _ => (null, null)
        };
    }

    public string? CustomRangeValue => CustomRange is { } custom
        ? custom.Start.ToString("o", CultureInfo.InvariantCulture) + ".." + custom.End.ToString("o", CultureInfo.InvariantCulture)
        : null;

    public static string RangeLabel(string? range) => range switch
    {
        Last7Days => "Last 7 days",
        Last30Days => "Last 30 days",
        Today => "Today",
        ThisMonth => "This month",
        _ => "Any time"
    };

    public static IEnumerable<string> AllRanges => Ranges;

    /// <summary>Query values for the active filters, for links that keep them.</summary>
    public IEnumerable<KeyValuePair<string, string>> ToQuery(bool includeSearchAndStatus = true)
    {
        if (includeSearchAndStatus && Search != null) yield return new("q", Search);
        if (Tag != null) yield return new("tag", Tag);
        if (Environment != null) yield return new("environment", Environment);
        if (Branch != null) yield return new("branch", Branch);
        if (Machine != null) yield return new("machine", Machine);
        if (includeSearchAndStatus && Status != null) yield return new("status", Status);
        if (CustomRangeValue is { } customRangeValue)
        {
            yield return new("range", Custom);
            yield return new("datetimerange", customRangeValue);
        }
        else
        {
            yield return new("range", Range);
        }
    }

    public static string ToQueryString(IEnumerable<KeyValuePair<string, string>> values)
    {
        var parts = values.Where(pair => pair.Value != null)
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))
            .ToList();
        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private static bool Matches(string? expected, string? actual) =>
        expected == null || string.Equals(expected, actual, StringComparison.Ordinal);

    private bool MatchesSearch(TestRunSummary record) =>
        new[] { record.Tag, record.Environment, record.BranchName, record.MachineName, record.Version, record.RecordId }
            .Any(value => Search != null && value?.Contains(Search, StringComparison.OrdinalIgnoreCase) == true);
}
