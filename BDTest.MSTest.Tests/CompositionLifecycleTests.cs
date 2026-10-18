using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest.Tests;

// Deliberately unrelated to the BDTest inheritance API.
public abstract class CompanyTestBase;

[BDTestClass, BDTestTag("fixture")]
public class CompositionLifecycleTests : CompanyTestBase, IBDTestMSTestTest
{
    private readonly BDTestRun _run = LifecycleProbe.CreateRun();
    private readonly BDTestScenario<LifecycleContext> _host;
    private TestContext _testContext = null!;
    private LifecycleProbe _probe = null!;

    public CompositionLifecycleTests() => _host = BDTestScenario.For<LifecycleContext>(this, _run);
    public BDTestBase BDTestHost => _host;
    public TestContext TestContext
    {
        get => _testContext;
        set { _testContext = value; MSTestBDTestLifecycle.Initialize(this, value); }
    }

    [TestInitialize]
    public Task Setup() => (_probe = new LifecycleProbe(_host, TestContext)).SetupAsync();

    [TestCleanup]
    public Task Teardown() => _probe.TeardownAsync();

    [BDTestRetrySetUp]
    private void RetrySetup() => _probe.RecordContext();

    [VerifyCompletionTestMethod(2), DataRow("first"), DataRow("second")]
    [ScenarioText("Native lifecycle"), TestInformation("Native lifecycle"), BDTestTag("native"), BDTestRetry(1)]
    public Task RowsAndRepeatsKeepIdentityAndOutput(string label) => _probe.ExecuteAsync(retry: true);

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public void SetupFailure(string label) { }

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public void BodyFailure(string label) => _probe.FailBody();

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public Task TeardownFailure(string label) => _probe.ExecuteAsync();

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public Task ContextDisposalFailure(string label) => _probe.ExecuteAsync();

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public void BodyAndDisposalFailure(string label) => _probe.FailBody();
}

[BDTestClass, BDTestTag("fixture")]
public class InheritanceLifecycleTests : MSTestBDTestBase<LifecycleContext>
{
    private readonly BDTestRun _run = LifecycleProbe.CreateRun();
    private LifecycleProbe _probe = null!;
    public override BDTestRun TestRun => _run;

    [TestInitialize]
    public Task Setup() => (_probe = new LifecycleProbe(this, TestContext!)).SetupAsync();

    [TestCleanup]
    public Task Teardown() => _probe.TeardownAsync();

    [BDTestRetrySetUp]
    private void RetrySetup() => _probe.RecordContext();

    [VerifyCompletionTestMethod(2), DataRow("first"), DataRow("second")]
    [ScenarioText("Native lifecycle"), TestInformation("Native lifecycle"), BDTestTag("native"), BDTestRetry(1)]
    public Task RowsAndRepeatsKeepIdentityAndOutput(string label) => _probe.ExecuteAsync(retry: true);

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public void SetupFailure(string label) { }

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public void BodyFailure(string label) => _probe.FailBody();

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public Task TeardownFailure(string label) => _probe.ExecuteAsync();

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public Task ContextDisposalFailure(string label) => _probe.ExecuteAsync();

    [VerifyCompletionTestMethod, DataRow("first"), DataRow("second")]
    public void BodyAndDisposalFailure(string label) => _probe.FailBody();
}

internal sealed class LifecycleProbe(AbstractContextBDTestBase<LifecycleContext> host, TestContext context)
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();
    private readonly List<LifecycleContext> _contexts = new();
    private readonly string _label = (string)context.TestData![0]!;
    private LifecycleContext _setupContext = null!;
    private string _id = null!;
    private static bool RunnerMode => Environment.GetEnvironmentVariable("BDTEST_CONFORMANCE_RUNNER") == "1";

    internal static BDTestRun CreateRun()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(MSTestBDTestLifecycle.OutcomeMap);
        return BDTestRun.Start(options);
    }

    internal async Task SetupAsync()
    {
        // The custom method attribute verifies AFTER the adapter's completion boundary.
        VerifyCompletionTestMethodAttribute.Current.Value!.Probe = this;
        await host.InitializeContextAsync();
        _setupContext = host.Context;
        RecordContext();
        _id = host.BDTestContext.CurrentScenarioBDTestExecutionId;
        Assert.IsTrue(Ids.TryAdd(_id, 0), "Rows and repeats must have unique execution IDs.");
        Assert.Contains(context.TestDisplayName!, _id);
        Console.WriteLine(_label + " setup");
        if (context.TestName is "ContextDisposalFailure" or "BodyAndDisposalFailure")
            _setupContext.FailDisposal = RunnerMode;
        if (RunnerMode && context.TestName == "SetupFailure")
            throw new InvalidOperationException("native setup failure");
    }

    internal void RecordContext()
    {
        host.Context.DisposalMessage = _label + " context disposal " + Guid.NewGuid().ToString("N");
        _contexts.Add(host.Context);
    }

    internal async Task ExecuteAsync(bool retry = false)
    {
        Assert.AreSame(_setupContext, host.Context);
        var attempts = 0;
        await Task.Yield();
        var scenario = await host.When(() =>
        {
            Console.WriteLine(_label + " step");
            if (retry && ++attempts == 1) throw new InvalidOperationException("Retry native lifecycle");
        }).Then(() => GC.KeepAlive(host.Context)).BDTestAsync();
        Assert.AreEqual(_id, scenario.FrameworkTestId);
        if (retry)
        {
            Assert.AreEqual(1, scenario.RetryCount);
            Assert.AreEqual("Native lifecycle", scenario.GetScenarioText());
            var information = scenario.CustomTestInformation;
            Assert.IsNotNull(information);
            Assert.AreEqual("Native lifecycle", information.Single().Information);
            Assert.Contains("native", scenario.Tags);
            Assert.Contains("fixture", scenario.Tags);
        }
    }

    internal void FailBody()
    {
        if (RunnerMode) throw new InvalidOperationException("native body failure");
    }

    internal async Task TeardownAsync()
    {
        await Task.Yield();
        Console.WriteLine(_label + " teardown");
        if (RunnerMode && context.TestName == "TeardownFailure")
            throw new InvalidOperationException("native teardown failure");
    }

    internal async Task VerifyCompletionAsync()
    {
        var run = host.TestRun;
        try
        {
            Assert.IsTrue(_contexts.Count > 0);
            Assert.IsTrue(_contexts.All(value => value.DisposalCalls == 1 && value.InitializationCalls == 1));
            var contexts = (IDictionary)typeof(AbstractContextBDTestBase<LifecycleContext>)
                .GetField("_contexts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
            Assert.AreEqual(0, contexts.Count);
            var output = (IDictionary)typeof(BDTestRun)
                .GetProperty("OutputBuffers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;
            Assert.AreEqual(0, output.Count);
            var store = typeof(BDTestRun).GetProperty("Store", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;
            foreach (var name in new[] { "PendingStartupOutput", "ScenarioListeners" })
            {
                var pending = (IDictionary)store.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
                Assert.IsFalse(pending.Contains(_id));
            }
            if (run.Scenarios.Count != 0)
            {
                var scenario = run.Scenarios.Single();
                Assert.AreEqual(_contexts.Count, scenario.RetryCount + 1);
                Assert.AreEqual(run.ReportId, scenario.ReportId);
                var startup = scenario.TestStartupInformation;
                var outputText = scenario.Output;
                var teardown = scenario.TearDownOutput;
                Assert.IsNotNull(startup);
                Assert.IsNotNull(outputText);
                Assert.IsNotNull(teardown);
                Assert.Contains(_label + " setup", startup);
                Assert.Contains(_label + " step", outputText);
                Assert.Contains(_label + " teardown", teardown);
                Assert.Contains(_contexts.Last().DisposalMessage, teardown);
                var other = _label == "first" ? "second" : "first";
                Assert.DoesNotContain(other, scenario.TestStartupInformation + scenario.Output + scenario.TearDownOutput);
                var report = BDTestJsonHelper.Deserialize(BDTestJsonHelper.GetTestJsonData(null, run));
                Assert.IsNotNull(report);
                Assert.IsNotNull(report.Scenarios);
                var reportedScenario = report.Scenarios.Single();
                Assert.AreEqual(run.ReportId, reportedScenario.ReportId);
                var reportedTeardown = reportedScenario.TearDownOutput;
                Assert.IsNotNull(reportedTeardown);
                Assert.Contains(_contexts.Last().DisposalMessage, reportedTeardown);
            }
            await host.EndTestAsync();
            Assert.IsTrue(_contexts.All(value => value.DisposalCalls == 1), "Repeated completion disposed twice.");
        }
        finally { run.Dispose(); }
    }
}

public sealed class LifecycleContext : IBDTestContextLifetime, IAsyncDisposable
{
    public int InitializationCalls { get; private set; }
    public int DisposalCalls { get; private set; }
    public bool FailDisposal { get; set; }
    public string DisposalMessage { get; set; } = "";

    public async ValueTask InitializeAsync() { await Task.Yield(); InitializationCalls++; }
    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        DisposalCalls++;
        Console.WriteLine(DisposalMessage);
        if (FailDisposal) throw new InvalidOperationException("native context disposal failure");
    }
}

public sealed class VerifyCompletionTestMethodAttribute : TestMethodAttribute
{
    private readonly int _repeats;
    internal static readonly AsyncLocal<CompletionCheck?> Current = new();
    internal sealed class CompletionCheck { internal LifecycleProbe? Probe; }

    public VerifyCompletionTestMethodAttribute(int repeats = 1,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = -1) : base(file, line) => _repeats = repeats;

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        var results = new List<TestResult>();
        var previous = Current.Value;
        try
        {
            for (var repeat = 0; repeat < _repeats; repeat++)
            {
                var check = new CompletionCheck();
                Current.Value = check;
                var result = await testMethod.InvokeAsync(null);
                Assert.IsNotNull(check.Probe, "Native initialization did not run.");
                await check.Probe.VerifyCompletionAsync();
                result.DisplayName = testMethod.TestMethodName + " repeat " + repeat;
                results.Add(result);
            }
            return results.ToArray();
        }
        finally { Current.Value = previous; }
    }
}
