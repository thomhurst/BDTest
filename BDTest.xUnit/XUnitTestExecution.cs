using System.Threading;

namespace BDTest.xUnit;

// Both adapter styles use one binding/capture/completion path. The native fixture
// also retains this object when construction or user setup/teardown throws.
internal sealed class XUnitTestExecution
{
    private readonly Action _begin;
    private readonly Func<ValueTask> _end;
    private readonly Action? _completeOutput;
    private bool _started;
    private int _completed;

    internal XUnitTestExecution(object owner, Action begin, Func<ValueTask> end,
        Action? completeOutput = null, BDTestRun? compositionRun = null)
    {
        _begin = begin;
        _end = end;
        _completeOutput = completeOutput;
        if (!XUnitBDTestLifecycle.Register(this, owner, compositionRun) && compositionRun != null)
            throw new InvalidOperationException("Register [assembly: AssemblyFixture(typeof(XUnitBDTestLifecycle))] before creating a BDTest host.");
    }

    internal void Begin()
    {
        if (_started || Volatile.Read(ref _completed) != 0) return;
        _begin();
        _started = true;
    }

    internal ValueTask CompleteAsync() => CompleteAsync(_end);

    internal ValueTask CompleteAsync(Func<ValueTask> complete)
    {
        if (Volatile.Read(ref _completed) != 0) return default;
        Begin();
        if (Interlocked.Exchange(ref _completed, 1) != 0) return default;
        _completeOutput?.Invoke();
        return complete();
    }
}
