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
    /// Registers a rule safely during concurrent evaluation. Rules added during
    /// an evaluation take effect on subsequent evaluations.
    /// </summary>
    public void Add(Func<Scenario, bool> condition)
    {
        _rules.Add(condition);
    }
}
