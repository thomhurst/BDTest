using System.Collections.Concurrent;
using Xunit;
using Xunit.v3;

namespace BDTest.xUnit;

/// <summary>Completes opted-in BDTest hosts after xUnit setup, body and teardown, including failures.</summary>
/// <remarks>Register with [assembly: AssemblyFixture(typeof(XUnitBDTestLifecycle))]. Runs remain caller-owned.</remarks>
public sealed class XUnitBDTestLifecycle : INotifyTestLifecycle, INotifyTestLifecycleAsync
{
    private const string KeyPrefix = "BDTest.xUnit.execution:";

    /// <inheritdoc />
    public void OnTestStarting(IXunitTest test) =>
        TestContext.Current.KeyValueStorage.TryAdd(KeyPrefix + test.UniqueID, new Execution());

    /// <inheritdoc />
    public void OnTestFinished(IXunitTest test) { }

    /// <inheritdoc />
    public ValueTask OnTestStartingAsync(IXunitTest test) => default;

    /// <inheritdoc />
    public async ValueTask OnTestFinishedAsync(IXunitTest test)
    {
        if (!TestContext.Current.KeyValueStorage.TryRemove(KeyPrefix + test.UniqueID, out var value)
            || value is not Execution execution) return;
        var errors = new List<Exception>();
        foreach (var host in execution.Hosts)
        {
            try { await host.CompleteAsync(); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    internal static bool Register(XUnitTestExecution host, object owner, BDTestRun? compositionRun)
    {
        var context = TestContext.Current;
        if (context.TestClassInstance != null && !ReferenceEquals(context.TestClassInstance, owner)) return false;
        if (context.Test == null || !context.KeyValueStorage.TryGetValue(KeyPrefix + context.Test.UniqueID, out var value)
            || value is not Execution execution)
            return false;
        lock (execution.Hosts)
        {
            // Reserve for the whole native test, even after manual completion:
            // reusing the ID would overwrite reports and revive old attachment writers.
            if (compositionRun != null && !execution.CompositionRuns.Add(compositionRun))
                throw new InvalidOperationException("Only one xUnit composition host can be created for each test and run.");
            execution.Hosts.Enqueue(host);
        }
        return true;
    }

    private sealed class Execution
    {
        internal ConcurrentQueue<XUnitTestExecution> Hosts { get; } = new();
        internal HashSet<BDTestRun> CompositionRuns { get; } = new();
    }
}
