using System.Runtime.CompilerServices;
using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class ScenarioRetentionTests : TestRunOwner
{
    [Before(HookType.Test)]
    public void RegisterOutcomeRulesAndExceptions()
    {
        // Global rules persist for the process lifetime, so never capture a fixture here.
        Options.SkipTestRules.Add(static scenario => scenario.BdTestBaseClass is Capture { ShouldSkip: true });
        Options.InconclusiveExceptionTypes.Add(typeof(InconclusiveOutcomeException));
        Options.SuccessExceptionTypes.Add(typeof(SuccessOutcomeException));
    }

    [Test]
    [Arguments("passed", Status.Passed)]
    [Arguments("failed", Status.Failed)]
    [Arguments("not-implemented", Status.NotImplemented)]
    [Arguments("inconclusive", Status.Inconclusive)]
    [Arguments("success-exception", Status.Passed)]
    [Arguments("skipped", Status.Skipped)]
    [Arguments("retry", Status.Passed)]
    [Arguments("retry-hook-failed", Status.Failed)]
    public async Task Completed_report_does_not_keep_fixture_context_or_text_closures_alive(string outcome, Status status)
    {
        var retained = ExecuteAndRelease(outcome);
        var json = ReportJson.SerializeObject(retained.Scenario);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(retained.Fixture.IsAlive).IsFalse().Because("Report retained the fixture");
        await Assert.That(retained.Context.IsAlive).IsFalse().Because("Report retained the context");
        await Assert.That(retained.Text.IsAlive).IsFalse().Because("Report retained the text callback");
        await Assert.That(BDTestUtil.GetScenarios(TestRun).Select(s => s.Guid)).Contains(Required(retained.Scenario.Guid));
        await Assert.That(retained.Scenario.Status).IsEqualTo(status);
        await Assert.That(Required(retained.Scenario.Steps).All(step => step.Runnable == null && step.OverriddenStepText == null)).IsTrue();
        await Assert.That(retained.Scenario.BdTestBaseClass).IsNull();
        await Assert.That(ReportJson.SerializeObject(retained.Scenario)).IsEqualTo(json);
        if (outcome == "retry")
        {
            await Assert.That(retained.Scenario.RetryCount).IsEqualTo(1);
            await Assert.That(retained.Scenario.Output).Contains("step output");
        }
    }

    [Test]
    public async Task Completed_scenario_can_still_access_a_live_fixture()
    {
        var fixture = new Capture(TestRun, "passed");
        var scenario = fixture.Run(new TextSource());
        await Assert.That(TestRun.Store.ScenariosByInternalId[scenario.Guid].BdTestBaseClass).IsSameReferenceAs(fixture);
        GC.KeepAlive(fixture);
    }

    [Test]
    public async Task ClearScenarios_discards_all_report_stores_and_pending_capture()
    {
        var fixture = new Capture(TestRun, "passed");
        var completed = fixture.Run(new TextSource());
        var pending = fixture.Build(new TextSource());
        var pendingId = Guid.NewGuid().ToString();
        var listenerCalls = 0;
        TestRun.Store.AppendStartupOutput(pendingId, "old setup", false);
        TestRun.Store.ListenForScenario(pendingId, _ => listenerCalls++);

        // Leave a step output buffer populated too, as can happen during an aborted run.
        var active = new Scenario(new List<Step>(), fixture.Build(new TextSource()).Definition);
        active.StartTime = DateTime.Now;
        active.Status = Status.Pending;
        TestOutputData.Run = TestRun;
        TestOutputData.TestId = active.Guid;
        var writer = new TestOutputData();
        writer.Write("old step output");
        await Assert.That(writer.ToString()).IsEqualTo("old step output");
        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun).Select(item => item.ScenarioId)).Contains(Required(pending.Guid));

        BDTestUtil.ClearScenarios(TestRun);
        BDTestUtil.ClearScenarios(TestRun); // Reset is idempotent.

        await Assert.That(BDTestUtil.GetScenarios(TestRun)).IsEmpty();
        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun)).IsEmpty();
        await Assert.That(TestRun.Store.ScenariosByTestFrameworkId).IsEmpty();
        await Assert.That(writer.ToString()).IsEmpty();
        var replacement = new Capture(TestRun, "passed", pendingId).Run(new TextSource());

        await Assert.That(replacement.TestStartupInformation).IsNullOrEmpty();
        await Assert.That(listenerCalls).IsZero();
        await Assert.That(BDTestUtil.GetScenarios(TestRun)).IsEquivalentTo(new[] { replacement }, EqualityComparer<BDTest.Contracts.ScenarioResult>.Create((left, right) => new KellermanSoftware.CompareNetObjects.CompareLogic().Compare(left, right).AreEqual, _ => 0), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(completed.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(completed.Steps)[1].StepText).IsEqualTo("Then retained text");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private (Scenario Scenario, WeakReference Fixture, WeakReference Context, WeakReference Text) ExecuteAndRelease(string outcome)
    {
        var fixture = new Capture(TestRun, outcome);
        var text = new TextSource();
        var context = fixture.Context;
        try
        {
            fixture.Run(text);

            RequireCondition(new[] { "passed", "skipped", "retry" }.Contains(outcome), "Expected an exceptional outcome");
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotImplementedException
            or InconclusiveOutcomeException or SuccessOutcomeException or ErrorOccurredDuringRetryActionException)
        {

            RequireCondition(new[] { "failed", "not-implemented", "inconclusive", "success-exception", "retry-hook-failed" }.Contains(outcome), "Unexpected exceptional outcome");
        }
        var scenario = TestRun.Store.ScenariosByTestFrameworkId[fixture.Id];
        return (scenario, new WeakReference(fixture), new WeakReference(context), new WeakReference(text));
    }

    private sealed class TextSource
    {
        public string GetText() => "retained text";
    }

    public sealed class CaptureContext { }

    private sealed class Capture(BDTestRun run, string outcome, string? id = null) : AbstractContextBDTestBase<CaptureContext>
    {
        public override BDTestRun TestRun => run;
        public string Id { get; } = id ?? Guid.NewGuid().ToString();
        public bool ShouldSkip => outcome == "skipped";
        protected override string BDTestExecutionId => Id;
        private int _attempts;

        public BDTest.Test.Steps.Then.Then Build(TextSource text) => When(() => Step(), callerMember: nameof(Run))
            .Then(() => GC.KeepAlive(Context)).WithStepText(() => text.GetText());

        [BDTestRetry(1)]
        public BDTest.Contracts.ScenarioResult Run(TextSource text)
        {
            BeginTest(Id, typeof(Capture).GetMethod(nameof(Run)));
            var builder = Build(text);
            return builder.BDTestAsync().GetAwaiter().GetResult();
        }

        public override Task OnBeforeRetry()
        {
            if (outcome == "retry-hook-failed") throw new InvalidOperationException("retry hook failed");
            return Task.CompletedTask;
        }

        private void Step()
        {
            Console.WriteLine("step output");
            _attempts++;
            switch (outcome)
            {
                case "failed": throw new InvalidOperationException("step failed");
                case "not-implemented": throw new NotImplementedException();
                case "inconclusive": throw new InconclusiveOutcomeException();
                case "success-exception": throw new SuccessOutcomeException();
                case "retry-hook-failed": throw new InvalidOperationException("retry requested");
                case "retry" when _attempts == 1: throw new InvalidOperationException("retry requested");
            }
        }
    }

    private sealed class InconclusiveOutcomeException : Exception { }
    private sealed class SuccessOutcomeException : Exception { }
}
