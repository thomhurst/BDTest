using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;

namespace BDTest.Test;

/// <summary>An immutable snapshot of table headers and cells for a step report.</summary>
public sealed class StepTable : StepArgument
{
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    [JsonConstructor]
    public StepTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (headers == null) throw new ArgumentNullException(nameof(headers));
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        Headers = Array.AsReadOnly(headers.Select(header => header ?? "").ToArray());
        Rows = Array.AsReadOnly(rows.Select(row =>
        {
            if (row == null || row.Count != Headers.Count)
                throw new ArgumentException("Each row must have the same number of cells as the headers.", nameof(rows));
            return (IReadOnlyList<string>)Array.AsReadOnly(row.Select(cell => cell ?? "").ToArray());
        }).ToArray());
    }

    /// <summary>Snapshots public readable instance properties, ordered by name. Indexers are excluded.</summary>
    public static StepTable From<T>(IEnumerable<T> rows)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
        return From(properties.Select(property => property.Name).ToArray(),
            rows.Select(row => properties.Select(property => row is null ? null : property.GetValue(row)).ToArray()).ToArray());
    }

    /// <summary>Snapshots explicit cells using invariant formatting. Null cells become empty strings.</summary>
    public static StepTable From(string[] headers, params object[][] rows)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        return new StepTable(headers, rows.Select(row => row == null ? null :
            (IReadOnlyList<string>)row.Select(cell => Convert.ToString(cell, CultureInfo.InvariantCulture) ?? "").ToArray()).ToArray());
    }

    public override string ToString() => $"(table: {Rows.Count} rows)";
}
