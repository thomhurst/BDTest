using System.Threading;
using BDTest.Test;

namespace BDTest.Output;

// The shared lifetime flag also reaches abandoned timeout tasks through ExecutionContext.
internal sealed class AttachmentScope : IDisposable
{
    private static readonly AsyncLocal<AttachmentScope?> CurrentScope = new();
    internal static AttachmentScope? Current => CurrentScope.Value;
    private readonly AttachmentScope? _previous;
    private bool _active = true;
    internal Scenario Scenario { get; }
    internal Step Step { get; }
    internal int AttemptNumber { get; }

    internal AttachmentScope(Scenario scenario, Step step)
    {
        Scenario = scenario;
        Step = step;
        AttemptNumber = scenario.RetryCount + 1;
        _previous = CurrentScope.Value;
        CurrentScope.Value = this;
    }

    // Caller holds the run's store gate, also used by Dispose.
    internal void ThrowIfInactive()
    {
        if (!_active) throw new InvalidOperationException("The attachment's step has already completed.");
    }

    public void Dispose()
    {
        lock (Scenario.ExecutionRun.Store.Gate) _active = false;
        CurrentScope.Value = _previous;
    }
}
