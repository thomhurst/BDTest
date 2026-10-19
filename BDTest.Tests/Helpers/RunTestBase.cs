global using BDTest.Tests.Helpers;

using BDTest.Interfaces;
using BDTest.TUnit;
using BDTest.Test;

namespace BDTest.Tests.Helpers;
// Each fixture owns its registrations and starts its run only after configuration.
public class TestRunOwner : IDisposable
{
    private BDTestRun? _run;
    public BDTestOptions Options { get; } = new();
    private readonly object _gate = new();
    public BDTestRun TestRun { get { lock (_gate) return _run ??= BDTestRun.Start(Options); } }
    public void Dispose() => _run?.Dispose();
}
public abstract class RunTestBase : BDTestBase, IDisposable
{
    private readonly TestRunOwner _owner = new();
    public BDTestOptions Options => _owner.Options;
    protected virtual bool BindCurrentTestMetadata => false;
    private bool _bound;
    public override BDTestRun TestRun
    {
        get
        {
            var run = _owner.TestRun;
            if (BindCurrentTestMetadata && !_bound)
            {
                _bound = true;
                BeginTest(BDTestExecutionId, TestContext.Current?.Metadata.TestDetails.MethodMetadata.GetReflectionInfo());
            }
            return run;
        }
    }
    public virtual void Dispose()
    {
        try { EndTest(); }
        finally { _owner.Dispose(); }
    }
}
public abstract class RunContextTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable where TContext : class
{
    private readonly TestRunOwner _owner = new();
    public BDTestOptions Options => _owner.Options;
    protected virtual bool BindCurrentTestMetadata => false;
    private bool _bound;
    public override BDTestRun TestRun
    {
        get
        {
            var run = _owner.TestRun;
            if (BindCurrentTestMetadata && !_bound)
            {
                _bound = true;
                BeginTest(BDTestExecutionId, TestContext.Current?.Metadata.TestDetails.MethodMetadata.GetReflectionInfo());
            }
            return run;
        }
    }
    public virtual void Dispose()
    {
        try { EndTest(); }
        finally { _owner.Dispose(); }
    }
}
public abstract class RunTUnitTestBase<TContext> : TUnitBDTestBase<TContext>, IDisposable where TContext : class
{
    private readonly TestRunOwner _owner = new();
    protected RunTUnitTestBase() => Options.RegisterFrameworkOutcomes(OutcomeMap);
    public BDTestOptions Options => _owner.Options;
    public override BDTestRun TestRun => _owner.TestRun;
    public virtual void Dispose()
    {
        try { EndTest(); }
        finally { _owner.Dispose(); }
    }
}

internal sealed class CallbackStepHook(Func<Step, Task>? before = null, Func<Step, Task>? after = null) : IStepHook
{
    public Task BeforeStepAsync(Step step) => before?.Invoke(step) ?? Task.CompletedTask;
    public Task AfterStepAsync(Step step) => after?.Invoke(step) ?? Task.CompletedTask;
}
