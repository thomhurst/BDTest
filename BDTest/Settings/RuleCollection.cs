using System.Threading;

namespace BDTest.Settings;

internal sealed class RuleCollection<T>
{
    private readonly object _sync = new();
    private T[] _rules = Array.Empty<T>();

    // Published arrays are never modified. Readers keep one snapshot throughout
    // evaluation, and user predicates run without holding the registration lock.
    internal IReadOnlyList<T> Snapshot => Volatile.Read(ref _rules);

    internal void Add(T rule)
    {
        lock (_sync)
        {
            var updated = new T[_rules.Length + 1];
            Array.Copy(_rules, updated, _rules.Length);
            updated[updated.Length - 1] = rule;
            Volatile.Write(ref _rules, updated);
        }
    }
}
