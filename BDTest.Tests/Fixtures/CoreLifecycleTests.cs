using BDTest.Output;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class CoreLifecycleTests : TestRunOwner
{
    [Test]
    public async Task CompletionClearsOnlyTheOwningRunAndCanBeRepeated()
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var firstFixture = new Probe(first);
        var secondFixture = new Probe(second);
        firstFixture.Start("same-id");
        var original = firstFixture.Context;
        Console.WriteLine("first startup");
        firstFixture.ScenarioHtmlWriter.Text("first pending html");
        secondFixture.Start("same-id");
        Console.WriteLine("second startup");
        secondFixture.ScenarioHtmlWriter.Text("second pending html");

        firstFixture.Finish();
        firstFixture.Finish();
        await Assert.That(TestOutputData.Run).IsSameReferenceAs(second);
        await Assert.That(TestOutputData.FrameworkExecutionId).IsEqualTo("same-id");
        var scenario = await secondFixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.TestStartupInformation).Contains("second startup").And.DoesNotContain("first");
        await Assert.That(scenario.CustomHtmlOutputForReport).Contains("second pending html");
        secondFixture.Finish();
        secondFixture.Finish();
        await Assert.That(TestOutputData.FrameworkExecutionId).IsNull();
        await Assert.That(TestOutputData.TestId).IsNull();
        await Assert.That(TestOutputData.ConstructionOutput).IsNull();
        await Assert.That(TestOutputData.Run).IsNull();

        firstFixture.Start("same-id");
        await Assert.That(firstFixture.Context).IsNotSameReferenceAs(original);
        var next = await firstFixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(next.TestStartupInformation).IsNullOrEmpty();

        await Assert.That(next.CustomHtmlOutputForReport).IsNullOrEmpty();
        firstFixture.Finish();
    }

    [Test]
    public async Task RepeatedCompletionDoesNotEndAnotherParallelExecutionOnTheSameFixture()
    {
        using var run = BDTestRun.Start();
        var fixture = new Probe(run);
        fixture.Start("first");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var other = Task.Run(async () =>
        {
            fixture.Start("second");
            var context = fixture.Context;
            fixture.WriteStartupOutput("second pending");
            started.SetResult();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(fixture.Context).IsSameReferenceAs(context);
            var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            await Assert.That(scenario.TestStartupInformation).Contains("second pending");
            fixture.Finish();
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Finish();
        fixture.Finish();
        completed.SetResult();
        await other;
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task InvalidIdsAreRejectedBeforeChangingCapture(string? id)
    {
        using var run = BDTestRun.Start();
        var fixture = new Probe(run);
        fixture.Start("valid");
        await Assert.That(() => fixture.Start(id!)).ThrowsExactly<ArgumentException>();
        await Assert.That(TestOutputData.FrameworkExecutionId).IsEqualTo("valid");
        fixture.Finish();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CrossFlowDisposalCapturesOnlyItsRunAndPreservesCallerOutput(bool failDisposal)
    {
        using var first = BDTestRun.Start();
        using var second = BDTestRun.Start();
        var fixture = new DisposalProbe(first);
        var firstScenario = await Task.Run(async () =>
        {
            fixture.Start("same-id");
            fixture.Context.FailDisposal = failDisposal;
            return await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        });
        var caller = new Probe(second);
        caller.Start("same-id");
        var secondScenario = await caller.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        TestOutputData.TestId = secondScenario.Guid;
        try
        {
            if (failDisposal)
            {
                var error = await Assert.That(async () => await fixture.EndTestAsync()).ThrowsExactly<InvalidOperationException>();
                await Assert.That(error!.Message).IsEqualTo("disposal failure");
            }
            else
            {
                await fixture.EndTestAsync();
            }
            await Assert.That(TestOutputData.Run).IsSameReferenceAs(second);
            await Assert.That(TestOutputData.FrameworkExecutionId).IsEqualTo("same-id");
            await Assert.That(TestOutputData.TestId).IsEqualTo(secondScenario.Guid);
            Console.WriteLine("caller output");

            await Assert.That(first.Store.ScenariosByInternalId[firstScenario.Guid].TearDownOutput).Contains("disposal output").And.DoesNotContain("caller output");

            await Assert.That(second.Scenarios.Single(s => s.Guid == secondScenario.Guid).TearDownOutput).Contains("caller output").And.DoesNotContain("disposal output");
            await Assert.That(first.OutputBuffers).IsEmpty();
            await fixture.EndTestAsync();

            await Assert.That((Required(first.Store.ScenariosByInternalId[firstScenario.Guid].TearDownOutput).Split("disposal output")).Length).IsEqualTo(2);
        }
        finally { await caller.EndTestAsync(); }
    }

    private sealed class DisposalProbe(BDTestRun run) : AbstractContextBDTestBase<DisposalContext>
    {
        public override BDTestRun TestRun => run;
        public void Start(string id) => BeginTest(id);
    }

    public sealed class DisposalContext : IAsyncDisposable
    {
        public bool FailDisposal { get; set; }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Console.WriteLine("disposal output");
            if (FailDisposal) throw new InvalidOperationException("disposal failure");
        }
    }

    private static void NoOp() { }

    [Test]
    public async Task LegacyOverrideKeepsCaptureIdentityAfterRepeatedStarts()
    {
        using var run = BDTestRun.Start();
        var fixture = new LegacyProbe(run);
        fixture.Start();
        fixture.Finish();
        fixture.Start();
        Console.WriteLine("legacy second startup");
        var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        await Assert.That(scenario.FrameworkTestId).IsEqualTo("legacy-id");
        await Assert.That(scenario.TestStartupInformation).Contains("legacy second startup");
        fixture.Finish();
    }

    [Test]
    public async Task DecoratedBaseIdsResolveOncePerExecution()
    {
        using var run = BDTestRun.Start();
        var fixture = new DecoratedProbe(run);
        foreach (var id in new[] { "first", "second" })
        {
            fixture.Start(id);
            Console.WriteLine("decorated startup");
            var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            await Assert.That(scenario.FrameworkTestId).IsEqualTo("prefix-" + id);
            await Assert.That(scenario.TestStartupInformation).Contains("decorated startup");
            fixture.Finish();
        }
    }

    private sealed class DecoratedProbe(BDTestRun run) : AbstractContextBDTestBase<MyTestContext>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "prefix-" + base.BDTestExecutionId;
        public void Start(string id) => BeginTest(id);
        public void Finish() => EndTest();
    }

    private sealed class LegacyProbe(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "legacy-id";
        // Bind delegates to exercise the obsolete bridge without suppressing diagnostics.
        public void Start() => Required(typeof(BDTestBase)
            .GetMethod("MarkTestAsStarted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            .CreateDelegate<Action>(this)();
        public void Finish() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }

    private sealed class Probe(BDTestRun run) : AbstractContextBDTestBase<MyTestContext>
    {
        public override BDTestRun TestRun => run;
        public void Start(string id) => BeginTest(id);
        public void Finish() => EndTest();
    }
}
