namespace BDTest.Settings.Retry;

public class RetryTestRules
{
    internal RetryTestRules()
    {
    }
        
    private readonly RuleCollection<RetryTestRule> _rules = new();

    internal IReadOnlyList<RetryTestRule> Rules => _rules.Snapshot;

    /// <summary>
    /// Registers an exception filter and default retry limit for subsequently started runs.
    /// Explicit retry attributes replace these defaults. A throwing predicate stops retries
    /// and records a diagnostic without replacing the original test failure.
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
