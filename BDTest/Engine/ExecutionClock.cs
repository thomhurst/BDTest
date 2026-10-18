using System.Diagnostics;

namespace BDTest.Engine;

// Keep legacy runtimes on Stopwatch; modern runs can supply a deterministic clock.
internal sealed class ExecutionClock
{
#if NET8_0_OR_GREATER
    internal static ExecutionClock System { get; } = new(TimeProvider.System);
    internal TimeProvider Provider { get; }
    internal ExecutionClock(TimeProvider provider) => Provider = provider;
    internal DateTime UtcNow => Provider.GetUtcNow().UtcDateTime;
    internal long GetTimestamp() => Provider.GetTimestamp();
    internal TimeSpan GetElapsedTime(long start) => Provider.GetElapsedTime(start);
#else
    internal static ExecutionClock System { get; } = new();
    internal DateTime UtcNow => DateTime.UtcNow;
    internal long GetTimestamp() => Stopwatch.GetTimestamp();
    internal TimeSpan GetElapsedTime(long start) => TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency);
#endif
    internal ExecutionTimer StartTimer() => new(this, GetTimestamp());
}

internal readonly struct ExecutionTimer(ExecutionClock clock, long startedAt)
{
    internal TimeSpan Elapsed => clock.GetElapsedTime(startedAt);
}
