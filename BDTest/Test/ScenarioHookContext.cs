using System.Threading;

namespace BDTest.Test;

/// <summary>The scenario, context and cancellation signal for one execution attempt.</summary>
public class ScenarioHookContext
{
    internal ScenarioHookContext(Scenario scenario, object? context, CancellationToken cancellationToken)
    {
        Scenario = scenario;
        Context = context;
        Attempt = scenario.RetryCount;
        CancellationToken = cancellationToken;
    }

    public Scenario Scenario { get; }
    /// <summary>The test's context, or null for a non-context test base.</summary>
    public object? Context { get; }
    /// <summary>Zero for the first attempt, then one, two and so on for retries.</summary>
    public int Attempt { get; }
    /// <summary>Cancelled when a step times out. Each attempt receives a fresh token.</summary>
    /// <remarks>Scenario hooks are awaited without a timeout; cleanup can ignore this signal.</remarks>
    public CancellationToken CancellationToken { get; }
}

/// <summary>An execution attempt with the test's typed context.</summary>
public sealed class ScenarioHookContext<TContext> : ScenarioHookContext
{
    internal ScenarioHookContext(Scenario scenario, TContext context, CancellationToken cancellationToken)
        : base(scenario, context, cancellationToken) => Context = context;

    public new TContext Context { get; }
}
