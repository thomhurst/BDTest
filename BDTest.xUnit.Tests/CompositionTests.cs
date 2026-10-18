using System.Collections.Concurrent;
using BDTest;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.xUnit;
using Xunit;

[assembly: AssemblyFixture(typeof(XUnitBDTestLifecycle))]
[assembly: AssemblyFixture(typeof(BDTest.xUnit.Tests.CompositionRuns))]

namespace BDTest.xUnit.Tests;

public sealed class CompositionRuns : IDisposable
{
    public ConcurrentBag<CompositionContext> Contexts { get; } = new();
    public BDTestRun First { get; } = Start();
    public BDTestRun Second { get; } = Start();

    private static BDTestRun Start()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(XUnitBDTestScenario.OutcomeMap);
        return BDTestRun.Start(options);
    }

    public void Dispose()
    {
        Assert.All(Contexts, context => Assert.True(context.Disposed, "Native fixture did not await context disposal."));
        var cleanupFile = Environment.GetEnvironmentVariable("BDTEST_CONFORMANCE_CLEANUP_FILE");
        if (!string.IsNullOrEmpty(cleanupFile)) File.WriteAllText(cleanupFile, Contexts.Count.ToString());
        foreach (var run in new[] { First, Second })
        {
            run.Dispose();
        }
    }
}

public abstract class CompanyTestBase
{
    [BDTestRetrySetUp]
    private void BeforeRetry() => RetryCalls++;
    public int RetryCalls { get; private set; }
}

[BDTestTag("composition owner")]
[Story(AsA = "tester", IWant = "composition metadata", SoThat = "reports name the owner")]
public class CompositionTests : CompanyTestBase, IAsyncLifetime
{
    private readonly BDTestScenario<CompositionContext> _host;
    private readonly string _id;
    private readonly CompositionRuns _runs;
    private static readonly ConcurrentDictionary<string, byte> RowIds = new();

    public CompositionTests(CompositionRuns runs)
    {
        _runs = runs;
        _host = XUnitBDTestScenario.For<CompositionContext>(this, runs.First);
        _id = TestContext.Current.Test!.UniqueID;
        _runs.Contexts.Add(_host.Context);
        Console.WriteLine("composition constructor:" + _id);
    }

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        _host.Context.Value = "setup:" + _id;
        Console.WriteLine("composition setup:" + _id);
    }

    public static IEnumerable<object[]> Rows => new[] { new object[] { new object() }, new object[] { new object() } };

    [Theory, MemberData(nameof(Rows), DisableDiscoveryEnumeration = true)]
    [ScenarioText("Composition row"), BDTestRetry(1)]
    public async Task RowsKeepOwnerIdentityContextOutputAndRetryHooks(object row)
    {
        Assert.True(RowIds.TryAdd(_id, 0), "Theory rows reused a framework identity.");
        var firstContext = _host.Context;
        await Task.Yield();
        var calls = 0;
        var scenario = await _host.When(() =>
        {
            Console.WriteLine("composition step:" + _id);
            if (++calls == 1) throw new InvalidOperationException("retry");
            Assert.NotSame(firstContext, _host.Context);
        }).Then(() => GC.KeepAlive(row)).BDTestAsync();
        Assert.Equal(1, RetryCalls);
        Assert.Equal(1, scenario.RetryCount);
        Assert.Equal(_id, scenario.FrameworkTestId);
        Assert.Equal("Composition row", scenario.GetScenarioText());
        Assert.Contains("composition owner", scenario.Tags);
        Assert.Contains("composition metadata", scenario.GetStoryText());
        Assert.Contains("composition constructor:" + _id, scenario.TestStartupInformation);
        Assert.Contains("composition setup:" + _id, scenario.TestStartupInformation);
        Assert.Contains("composition step:" + _id, scenario.Steps.First().Output);
    }

    [Fact]
    public async Task DuplicateHostIsRejectedWithoutChangingLiveOutputOrAttachments()
    {
        var context = _host.Context;
        var attachments = _host.Attachments;
        var error = Assert.Throws<InvalidOperationException>(() =>
            XUnitBDTestScenario.For<object>(this, _runs.First));
        Assert.Contains("Only one xUnit composition host", error.Message);
        Assert.Same(context, _host.Context);
        Assert.False(context.Disposed);

        var scenario = await _host.When(() => Console.WriteLine("original host step"))
            .Then(() => GC.KeepAlive(this)).BDTestAsync();
        _host.WriteTearDownOutput("original host teardown");
        attachments.Attach("original.txt", new byte[] { 1 }, "text/plain");
        Assert.Contains("original host step", scenario.Steps[0].Output);
        Assert.Contains("original host teardown", scenario.TearDownOutput);
        Assert.Single(scenario.Attachments);
        Assert.Equal(_id, scenario.FrameworkTestId);
    }

    [Fact]
    public async Task CompletedHostCannotBeReplacedInSameRun()
    {
        using var run = StartRun();
        var host = XUnitBDTestScenario.For<object>(this, run);
        var attachments = host.Attachments;
        var scenario = await host.When(() => GC.KeepAlive(this)).Then(() => GC.KeepAlive(this)).BDTestAsync();
        await host.EndTestAsync();

        Assert.Throws<InvalidOperationException>(() => XUnitBDTestScenario.For<object>(this, run));
        Assert.Throws<InvalidOperationException>(() => attachments.Attach("late.txt", new byte[] { 1 }, "text/plain"));
        Assert.Same(scenario, Assert.Single(run.Scenarios));
    }

    [Fact]
    public async Task TwoExplicitRunsKeepSameFrameworkIdIsolated()
    {
        // A second host uses the same runner identity, with independently owned state.
        using var secondRun = StartRun();
        var second = XUnitBDTestScenario.For<CompositionContext>(this, secondRun);
        var firstContext = _host.Context;
        Console.WriteLine("second startup");
        var secondContext = second.Context;
        secondContext.Value = "second";
        var secondScenario = await second.When(() => Console.WriteLine("second step")).Then(() => GC.KeepAlive(this)).BDTestAsync();
        await second.EndTestAsync();
        _host.WriteStartupOutput("first startup");
        var firstScenario = await _host.When(() => Assert.Same(firstContext, _host.Context))
            .Then(() => Assert.Equal("setup:" + _id, _host.Context.Value)).BDTestAsync();
        Assert.Equal(firstScenario.FrameworkTestId, secondScenario.FrameworkTestId);
        Assert.NotSame(firstContext, secondContext);
        Assert.True(secondContext.Disposed);
        Assert.Contains("first startup", firstScenario.TestStartupInformation);
        Assert.DoesNotContain("second startup", firstScenario.TestStartupInformation);
        Assert.DoesNotContain("first startup", secondScenario.TestStartupInformation);
        Assert.Single(secondRun.Scenarios);
        var report = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), secondRun);
        Assert.Contains("second startup", report);
        Assert.DoesNotContain("first startup", report);
    }

    private static BDTestRun StartRun()
    {
        var options = new BDTestOptions();
        options.RegisterFrameworkOutcomes(XUnitBDTestScenario.OutcomeMap);
        return BDTestRun.Start(options);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        Console.WriteLine("composition teardown:" + _id);
        _runs.Contexts.Add(_host.Context);
        // Completion belongs to XUnitBDTestLifecycle, after user teardown.
    }
}

public sealed class CompositionContext : IAsyncDisposable
{
    public string Value { get; set; } = string.Empty;
    public bool Disposed { get; private set; }
    public bool FailDisposal { get; set; }
    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        Assert.False(Disposed);
        Disposed = true;
        if (FailDisposal) throw new InvalidOperationException("native ContextDisposalFailure");
    }
}
