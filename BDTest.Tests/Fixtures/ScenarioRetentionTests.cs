using System.Runtime.CompilerServices;
using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ScenarioRetentionTests
{
    [OneTimeSetUp]
    public void RegisterOutcomeRulesAndExceptions()
    {
        // Global rules persist for the process lifetime, so never capture a fixture here.
        BDTestSettings.GlobalSkipTestRules.Add(static scenario => scenario.BdTestBaseClass is Capture { ShouldSkip: true });
        BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(InconclusiveOutcomeException));
        BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(SuccessOutcomeException));
    }

    [TestCase("passed", Status.Passed)]
    [TestCase("failed", Status.Failed)]
    [TestCase("not-implemented", Status.NotImplemented)]
    [TestCase("inconclusive", Status.Inconclusive)]
    [TestCase("success-exception", Status.Passed)]
    [TestCase("skipped", Status.Skipped)]
    [TestCase("retry", Status.Passed)]
    [TestCase("retry-hook-failed", Status.Inconclusive)]
    public void Completed_report_does_not_keep_fixture_context_or_text_closures_alive(string outcome, Status status)
    {
        var retained = ExecuteAndRelease(outcome);
        var json = JsonConvert.SerializeObject(retained.Scenario);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(retained.Fixture.IsAlive, Is.False, "Report retained the fixture");
        Assert.That(retained.Context.IsAlive, Is.False, "Report retained the context");
        Assert.That(retained.Text.IsAlive, Is.False, "Report retained the text callback");
        Assert.That(BDTestUtil.GetScenarios(), Does.Contain(retained.Scenario));
        Assert.That(retained.Scenario.Status, Is.EqualTo(status));
        Assert.That(retained.Scenario.Steps.All(step => step.Runnable == null && step.OverriddenStepText == null), Is.True);
        Assert.That(retained.Scenario.BdTestBaseClass, Is.Null);
        Assert.That(JsonConvert.SerializeObject(retained.Scenario), Is.EqualTo(json));
        if (outcome == "retry")
        {
            Assert.That(retained.Scenario.RetryCount, Is.EqualTo(1));
            Assert.That(retained.Scenario.Output, Does.Contain("step output"));
        }
    }

    [Test]
    public void Completed_scenario_can_still_access_a_live_fixture()
    {
        var fixture = new Capture("passed");
        var scenario = fixture.Run(new TextSource());
        Assert.That(scenario.BdTestBaseClass, Is.SameAs(fixture));
        GC.KeepAlive(fixture);
    }

    [Test]
    public void ClearScenarios_discards_all_report_stores_and_pending_capture()
    {
        var fixture = new Capture("passed");
        var completed = fixture.Run(new TextSource());
        var pending = fixture.Build(new TextSource());
        var pendingId = Guid.NewGuid().ToString();
        var listenerCalls = 0;
        TestHolder.AppendStartupOutput(pendingId, "old setup", false);
        TestHolder.ListenForScenario(pendingId, _ => listenerCalls++);

        // Leave a step output buffer populated too, as can happen during an aborted run.
        var active = new Scenario(new List<Step>(), fixture.Build(new TextSource()));
        active.StartTime = DateTime.Now;
        TestOutputData.TestId = active.Guid;
        var writer = new TestOutputData();
        writer.Write("old step output");
        Assert.That(writer.ToString(), Is.EqualTo("old step output"));
        Assert.That(BDTestUtil.GetNotRunScenarios(), Does.Contain(pending));

        BDTestUtil.ClearScenarios();
        BDTestUtil.ClearScenarios(); // Reset is idempotent.

        Assert.That(BDTestUtil.GetScenarios(), Is.Empty);
        Assert.That(BDTestUtil.GetNotRunScenarios(), Is.Empty);
        Assert.That(TestHolder.ScenariosByTestFrameworkId, Is.Empty);
        Assert.That(writer.ToString(), Is.Empty);
        var replacement = new Capture("passed", pendingId).Run(new TextSource());
        Assert.That(replacement.TestStartupInformation, Is.Null.Or.Empty);
        Assert.That(listenerCalls, Is.Zero);
        Assert.That(BDTestUtil.GetScenarios(), Is.EqualTo(new[] { replacement }));
        Assert.That(completed.Status, Is.EqualTo(Status.Passed));
        Assert.That(completed.Steps[1].StepText, Is.EqualTo("Then retained text"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Scenario Scenario, WeakReference Fixture, WeakReference Context, WeakReference Text) ExecuteAndRelease(string outcome)
    {
        var fixture = new Capture(outcome);
        var text = new TextSource();
        var context = fixture.Context;
        try
        {
            fixture.Run(text);
            Assert.That(outcome, Is.AnyOf("passed", "skipped", "retry"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotImplementedException
            or InconclusiveOutcomeException or SuccessOutcomeException or ErrorOccurredDuringRetryActionException)
        {
            Assert.That(outcome, Is.AnyOf("failed", "not-implemented", "inconclusive", "success-exception", "retry-hook-failed"));
        }
        var scenario = TestHolder.ScenariosByTestFrameworkId[fixture.Id];
        return (scenario, new WeakReference(fixture), new WeakReference(context), new WeakReference(text));
    }

    private sealed class TextSource
    {
        public string GetText() => "retained text";
    }

    public sealed class CaptureContext { }

    private sealed class Capture(string outcome, string id = null) : AbstractContextBDTestBase<CaptureContext>
    {
        public string Id { get; } = id ?? Guid.NewGuid().ToString();
        public bool ShouldSkip => outcome == "skipped";
        protected override string BDTestExecutionId => Id;
        private int _attempts;

        public BDTest.Test.Steps.Then.Then Build(TextSource text) => When(() => Step(), callerMember: nameof(Run))
            .Then(() => GC.KeepAlive(Context)).WithStepText(() => text.GetText());

        [BDTestRetry(1)]
        public Scenario Run(TextSource text)
        {
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
