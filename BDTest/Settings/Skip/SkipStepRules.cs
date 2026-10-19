using BDTest;

namespace BDTest.Settings.Skip;

public class SkipStepRules
{
    internal SkipStepRules()
    {
    }
        
    private readonly RuleCollection<SkipStepRule<object>> _rules = new();

    internal IReadOnlyList<SkipStepRule<object>> Rules => _rules.Snapshot;

    /// <summary>
    /// Registers a rule safely during concurrent evaluation. Rules added during
    /// an evaluation take effect on subsequent evaluations.
    /// </summary>
    public void Add<T>(Func<T, bool> condition) where T : SkipStepAttribute
    {
        // Cast to Func<object, bool>
        Func<object, bool> func = value => condition((T) value);
        _rules.Add(new SkipStepRule<object>(typeof(T), func));
    }
}
