namespace BDTest.Settings.Retry;

public class RetryTestRules
{
    internal RetryTestRules()
    {
    }
        
    private readonly RuleCollection<RetryTestRule> _rules = new();

    internal IReadOnlyList<RetryTestRule> Rules => _rules.Snapshot;

    /// <summary>
    /// Registers a rule safely during concurrent evaluation. Rules added during
    /// an evaluation take effect on subsequent evaluations.
    /// </summary>
    public void Add(Func<Exception, bool> condition, int retryLimit)
    {
        _rules.Add(new RetryTestRule(condition, retryLimit));
    }
}

public class RetryTestRule
{
    internal Func<Exception, bool> Condition { get; }
    public int RetryLimit { get; }

    internal RetryTestRule(Func<Exception, bool> condition, int retryLimit)
    {
        Condition = condition;
        RetryLimit = retryLimit;
    }
}
