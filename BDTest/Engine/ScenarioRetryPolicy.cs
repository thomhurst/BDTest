using BDTest.Settings.Retry;
using BDTest.Test;

namespace BDTest.Engine;

// Selected once before execution. Attribute metadata already implements method/class/assembly precedence.
internal sealed class ScenarioRetryPolicy
{
    private readonly int? _attributeCount;
    private readonly RetryTestRule[] _rules;

    private ScenarioRetryPolicy(int? attributeCount, RetryTestRule[] rules)
    {
        _attributeCount = attributeCount;
        _rules = rules;
    }

    internal static ScenarioRetryPolicy Resolve(Scenario scenario)
    {
        var count = scenario.TestMetadata?.RetryCount;
        return new ScenarioRetryPolicy(count, count.HasValue
            ? Array.Empty<RetryTestRule>()
            : scenario.ExecutionRun.Options.RetryRules.ToArray());
    }

    internal bool ShouldRetry(Exception exception, int retriesPerformed)
    {
        if (_attributeCount.HasValue) return retriesPerformed < _attributeCount.Value;

        var limit = 0;
        // Evaluate every filter before deciding: even a later throwing predicate stops retrying.
        foreach (var rule in _rules)
            if (rule.Condition(exception)) limit = Math.Max(limit, rule.RetryLimit);
        return retriesPerformed < limit;
    }
}
