using BDTest.Test;
using Xunit;
using Xunit.v3;

namespace BDTest.xUnit.Tests;

// Real runner failures are enabled only by Test-AdapterConformance.ps1, which
// checks both their TRX errors and assembly-fixture cleanup assertions.
public class NativeCompositionFailureTests : CompanyTestBase, IAsyncLifetime
{
    private readonly BDTestScenario<CompositionContext> _host;
    private readonly string _method = ((IXunitTestMethod)TestContext.Current.TestMethod!).Method.Name;
    private static bool RunnerMode => Environment.GetEnvironmentVariable("BDTEST_CONFORMANCE_RUNNER") == "1";

    public NativeCompositionFailureTests(CompositionRuns runs)
    {
        _host = XUnitBDTestScenario.For<CompositionContext>(this, runs.First);
        runs.Contexts.Add(_host.Context);
        if (_method == nameof(ContextDisposalFailure))
        {
            _host.Context.FailDisposal = RunnerMode;
            var second = XUnitBDTestScenario.For<CompositionContext>(this, runs.Second);
            runs.Contexts.Add(second.Context);
        }
        FailAt(nameof(ConstructorFailure));
    }

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        FailAt(nameof(SetupFailure));
    }

    [Fact] public Task ConstructorFailure() => Execute();
    [Fact] public Task SetupFailure() => Execute();
    [Fact] public Task BodyFailure() => Execute();
    [Fact] public Task TeardownFailure() => Execute();
    [Fact] public Task ContextDisposalFailure() => Execute();

    private Task Execute() => _host.When(() => FailAt(nameof(BodyFailure)))
        .Then(() => GC.KeepAlive(this)).BDTestAsync(global::Xunit.TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        FailAt(nameof(TeardownFailure));
    }

    private void FailAt(string method)
    {
        if (RunnerMode && _method == method) throw new InvalidOperationException("native " + method);
    }
}

public class NativeInheritanceFailureTests : XUnitBDTestBase<CompositionContext>
{
    private readonly BDTestRun _run;
    public override BDTestRun TestRun => _run;
    private readonly string _method = ((IXunitTestMethod)TestContext.Current.TestMethod!).Method.Name;
    private static bool RunnerMode => Environment.GetEnvironmentVariable("BDTEST_CONFORMANCE_RUNNER") == "1";

    public NativeInheritanceFailureTests(CompositionRuns runs)
    {
        _run = runs.Second;
        runs.Contexts.Add(Context);
        FailAt(nameof(ConstructorFailure));
    }

    public override async ValueTask InitializeAsync()
    {
        await Task.Yield();
        FailAt(nameof(SetupFailure));
    }

    [Fact] public Task ConstructorFailure() => Execute();
    [Fact] public Task SetupFailure() => Execute();
    [Fact] public Task BodyFailure() => Execute();
    [Fact] public Task TeardownFailure() => Execute();

    private Task Execute() => When(() => FailAt(nameof(BodyFailure)))
        .Then(() => GC.KeepAlive(this)).BDTestAsync(global::Xunit.TestContext.Current.CancellationToken);

    public override async ValueTask DisposeAsync()
    {
        await Task.Yield();
        FailAt(nameof(TeardownFailure));
        // Native lifecycle completes even overrides that never call base.
    }

    private void FailAt(string method)
    {
        if (RunnerMode && _method == method) throw new InvalidOperationException("native " + method);
    }
}
