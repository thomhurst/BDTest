namespace BDTest.Output;

// Shared by every continuation of one user callback, including abandoned work.
internal sealed class ExecutionOutputScope : IDisposable
{
    private static readonly AsyncLocal<ExecutionOutputScope?> Current = new();
    private readonly ExecutionOutputScope? _previous = Current.Value;
    private int _closed;
    private readonly BDTestRun _run;

    internal static bool IsDetached => Current.Value?.Detached == true;
    private bool Detached => Volatile.Read(ref _closed) != 0 || _previous?.Detached == true;

    internal ExecutionOutputScope(BDTestRun run)
    {
        _run = run;
        Current.Value = this;
    }

    internal static void Commit(Action publish)
    {
        var scope = Current.Value;
        if (scope == null) { publish(); return; }
        lock (scope._run.Store.Gate)
        {
            if (!scope.Detached) publish();
        }
    }

    public void Dispose()
    {
        lock (_run.Store.Gate) Interlocked.Exchange(ref _closed, 1);
        Current.Value = _previous;
    }
}
