using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using BDTest.TestUtilities;

namespace BDTest.TUnit.Tests;

// Deliberately unrelated to the BDTest inheritance API.
public abstract class CompanyTestBase;

[Arguments("first")]
[Arguments("second")]
[BDTestTag("fixture")]
public class CompositionLifecycleTests : CompanyTestBase, IBDTestTUnitTest
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();
    private readonly string _label;
    private readonly BDTestRun _run;
    private readonly BDTestScenario<CompositionContext> _host;
    private readonly List<CompositionContext> _contexts = new();
    private CompositionContext? _context;

    public CompositionLifecycleTests(string label)
    {
        _label = label;
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(TUnitBDTestLifecycle.OutcomeMap);
        _run = BDTestRun.Start(options);
        _host = BDTestScenario.For<CompositionContext>(this, _run);
    }

    public BDTestBase BDTestHost => _host;
    private static bool RunnerMode => Environment.GetEnvironmentVariable("BDTEST_CONFORMANCE_RUNNER") == "1";

    [Before(HookType.Test)]
    public async Task Setup()
    {
        await _host.InitializeContextAsync();
        _context = _host.Context;
        _contexts.Add(_context);
        Console.WriteLine(_label + " setup");
        _host.ScenarioHtmlWriter.Text(_label + " html");
        if (RunnerMode && TestContext.Current!.Metadata.TestDetails.MethodName == nameof(SetupFailure))
            throw new InvalidOperationException("composition setup failure");
    }

    [After(HookType.Test)]
    public async Task Teardown()
    {
        await Task.Yield();
        Console.WriteLine(_label + " teardown");
        if (RunnerMode && TestContext.Current!.Metadata.TestDetails.MethodName == nameof(TeardownFailure))
            throw new InvalidOperationException("composition teardown failure");
    }

    [Test, Arguments(1), Arguments(2), Repeat(1)]
    public async Task RowsAndRepeatsKeepIdentityAndOutput(int row)
    {
        var id = TestContext.Current!.Id;
        await Assert.That(Ids.TryAdd(id, 0)).IsTrue();
        await Task.Yield();
        var scenario = await _host.When(() => Console.WriteLine(_label + " step " + row))
            .Then(() => GC.KeepAlive(_host.Context)).BDTestAsync();
        await Assert.That(scenario.FrameworkTestId).IsEqualTo(id);
        await Assert.That(ReferenceEquals(_context, _host.Context)).IsTrue();
        await Assert.That(_context!.InitializationCalls).IsEqualTo(1);
    }

    [Test, ScenarioText("Composition metadata"), TestInformation("Composition metadata")]
    [BDTestTag("Composition metadata"), BDTestRetry(1)]
    public Task MethodMetadataAndRetry() => MethodIdentityContract.Verify(_host, "Composition metadata", true);

    [BDTestRetrySetUp]
    private void RecordRetryContext() => _contexts.Add(_host.Context);

    [Test]
    public void SetupFailure() { }

    [Test]
    public void BodyFailure()
    {
        if (RunnerMode) throw new InvalidOperationException("composition body failure");
    }

    [Test]
    public void ContextDisposalFailure() => _host.Context.FailDisposal = RunnerMode;

    [Test]
    public Task TeardownFailure() => _host.When(() => Console.WriteLine(_label + " step"))
        .Then(() => GC.KeepAlive(_host.Context)).BDTestAsync();

    internal async Task VerifyCompletion()
    {
        try
        {
            await Assert.That(_contexts.Count > 0).IsTrue();
            await Assert.That(_contexts.All(context => context.DisposalCalls == 1)).IsTrue();
            var contexts = (IDictionary)typeof(AbstractContextBDTestBase<CompositionContext>)
                .GetField("_contexts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_host)!;
            await Assert.That(contexts.Count).IsEqualTo(0);
            var output = (IDictionary)typeof(BDTestRun)
                .GetProperty("OutputBuffers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_run)!;
            await Assert.That(output.Count).IsEqualTo(0);
            var store = typeof(BDTestRun).GetProperty("Store", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_run)!;
            var id = TestContext.Current!.Id;
            foreach (var name in new[] { "PendingStartupOutput", "ScenarioListeners" })
            {
                var pending = (IDictionary)store.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
                await Assert.That(pending.Contains(id)).IsFalse();
            }
            if (_run.Scenarios.Count != 0)
            {
                var scenario = _run.Scenarios.Single();
                await Assert.That(_contexts.Count).IsEqualTo(scenario.RetryCount + 1);
                var other = _label == "first" ? "second" : "first";
                await Assert.That(scenario.ReportId).IsEqualTo(_run.ReportId);
                await Assert.That(scenario.TestStartupInformation).Contains(_label + " setup");
                if (scenario.GetScenarioText() != "Composition metadata")
                    await Assert.That(scenario.Output).Contains(_label + " step");
                await Assert.That(scenario.TearDownOutput).Contains(_label + " teardown");
                await Assert.That(scenario.TestStartupInformation + scenario.Output + scenario.TearDownOutput).DoesNotContain(other);
                var report = BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, _run))
                    ?? throw new InvalidOperationException("Expected the composition run's report.");
                var scenarios = report.Scenarios ?? throw new InvalidOperationException("Expected composition report scenarios.");
                await Assert.That(scenarios.Single().ReportId).IsEqualTo(_run.ReportId);
            }
        }
        finally { _run.Dispose(); }
    }
}

public sealed class CompositionContext : IBDTestContextLifetime, IAsyncDisposable
{
    public int InitializationCalls { get; private set; }
    public int DisposalCalls { get; private set; }
    public bool FailDisposal { get; set; }

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        InitializationCalls++;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        DisposalCalls++;
        if (FailDisposal) throw new InvalidOperationException("composition context disposal failure");
    }
}

public static class CompositionCompletionChecks
{
    [AfterEvery(HookType.Test, Order = int.MaxValue)]
    public static Task VerifyCompletion(TestContext context) =>
        context.Metadata.TestDetails.ClassInstance is CompositionLifecycleTests test
            ? test.VerifyCompletion() : Task.CompletedTask;
}
