using System.Collections.ObjectModel;

namespace BDTest.Contracts;

internal static class ContractCollections
{
    internal static IReadOnlyList<T> List<T>(IReadOnlyList<T>? values)
    {
        if (values == null) return Array.AsReadOnly(Array.Empty<T>());
        var snapshot = values.ToArray();
        if (snapshot.Any(value => value is null))
            throw new ArgumentException("Collection entries must not be null.", nameof(values));
        return Array.AsReadOnly(snapshot);
    }

    internal static IReadOnlyDictionary<string, string> Dictionary(IReadOnlyDictionary<string, string>? values)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        if (values != null)
        {
            foreach (var pair in values)
            {
                if (pair.Value == null)
                    throw new ArgumentException("Dictionary values must not be null.", nameof(values));
                snapshot.Add(pair.Key, pair.Value);
            }
        }
        return new ReadOnlyDictionary<string, string>(snapshot);
    }
}
