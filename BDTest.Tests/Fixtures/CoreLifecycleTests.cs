using BDTest.Output;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class CoreLifecycleTests
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
        Assert.That(TestOutputData.Run, Is.SameAs(second));
        Assert.That(TestOutputData.FrameworkExecutionId, Is.EqualTo("same-id"));
        var scenario = await secondFixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(scenario.TestStartupInformation, Does.Contain("second startup").And.Not.Contain("first"));
        Assert.That(scenario.CustomHtmlOutputForReport, Does.Contain("second pending html"));
        secondFixture.Finish();
        secondFixture.Finish();
        Assert.That(TestOutputData.FrameworkExecutionId, Is.Null);
        Assert.That(TestOutputData.TestId, Is.Null);
        Assert.That(TestOutputData.ConstructionOutput, Is.Null);
        Assert.That(TestOutputData.Run, Is.SameAs(BDTestRun.Default));

        firstFixture.Start("same-id");
        Assert.That(firstFixture.Context, Is.Not.SameAs(original));
        var next = await firstFixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        Assert.That(next.TestStartupInformation, Is.Null.Or.Empty);
        Assert.That(next.CustomHtmlOutputForReport, Is.Null.Or.Empty);
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
            Assert.That(fixture.Context, Is.SameAs(context));
            var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            Assert.That(scenario.TestStartupInformation, Does.Contain("second pending"));
            fixture.Finish();
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Finish();
        fixture.Finish();
        completed.SetResult();
        await other;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void InvalidIdsAreRejectedBeforeChangingCapture(string id)
    {
        using var run = BDTestRun.Start();
        var fixture = new Probe(run);
        fixture.Start("valid");
        Assert.Throws<ArgumentException>(() => fixture.Start(id));
        Assert.That(TestOutputData.FrameworkExecutionId, Is.EqualTo("valid"));
        fixture.Finish();
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
        Assert.That(scenario.FrameworkTestId, Is.EqualTo("legacy-id"));
        Assert.That(scenario.TestStartupInformation, Does.Contain("legacy second startup"));
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
            Assert.That(scenario.FrameworkTestId, Is.EqualTo("prefix-" + id));
            Assert.That(scenario.TestStartupInformation, Does.Contain("decorated startup"));
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
