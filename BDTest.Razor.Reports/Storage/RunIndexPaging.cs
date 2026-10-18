using System.Globalization;
using System.Text;

namespace BDTest.Razor.Reports.Storage;

/// <summary>Shared keyset paging for in-memory and compatibility stores.</summary>
public static class RunIndexPaging
{
    public static RunIndexPage Apply(IEnumerable<RunIndexEntry> entries, RunIndexQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        var filterKey = Encode(query.BranchName) + ":" + Encode(query.Environment) + ":" + Encode(query.Tag);
        long ticks = long.MaxValue;
        string? id = null;
        if (query.ContinuationToken != null)
        {
            try
            {
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(query.ContinuationToken)).Split('|');
                if (parts.Length != 4 || parts[0] != "1" || parts[1] != filterKey) throw new FormatException();
                ticks = long.Parse(parts[2], CultureInfo.InvariantCulture);
                if (ticks < 0 || ticks > DateTime.MaxValue.Ticks) throw new FormatException();
                id = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                throw new ArgumentException("Invalid continuation token for this query.", nameof(query), exception);
            }
        }
        var page = entries.Where(entry =>
                (query.BranchName == null || entry.BranchName == query.BranchName) &&
                (query.Environment == null || entry.Environment == query.Environment) &&
                (query.Tag == null || entry.Tag == query.Tag) &&
                (id == null || entry.StartedAtDateTime.Ticks < ticks ||
                 (entry.StartedAtDateTime.Ticks == ticks && StringComparer.Ordinal.Compare(entry.RecordId, id) > 0)))
            .OrderByDescending(entry => entry.StartedAtDateTime)
            .ThenBy(entry => entry.RecordId, StringComparer.Ordinal)
            .Take(query.PageSize + 1).ToArray();
        var results = page.Take(query.PageSize).ToArray();
        string? continuation = null;
        if (page.Length > query.PageSize)
        {
            var last = results[^1];
            continuation = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                "1|" + filterKey + "|" + last.StartedAtDateTime.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + Encode(last.RecordId)));
        }
        return new RunIndexPage { Entries = results, ContinuationToken = continuation };
    }

    private static string Encode(string? value) => value == null ? "~" : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
