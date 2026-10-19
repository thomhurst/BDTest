using BDTest.Test;

namespace BDTest.Settings.Skip;

public class SkipTestRules
{
    internal SkipTestRules()
    {
    }
        
    private readonly RuleCollection<Func<Scenario, bool>> _rules = new();

    internal IReadOnlyList<Func<Scenario, bool>> Rules => _rules.Snapshot;

    /// <summary>
    /// Registers a scenario predicate for subsequently started runs. Predicates are evaluated
    /// once before the first attempt, stopping at the first match. A throwing predicate fails
    /// the scenario without retries.
    /// </summary>
    public void Add(Func<Scenario, bool> condition)
    {
        _rules.Add(condition);
    }
}
