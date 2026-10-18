using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>An immutable table, including independent snapshots of every row.</summary>
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
}
