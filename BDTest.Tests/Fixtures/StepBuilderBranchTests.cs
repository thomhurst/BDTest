using BDTest.Attributes;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class StepBuilderBranchTests : BDTestBase
{
    [Test]
    public async Task Branches_execute_only_their_own_steps_and_are_reported_separately()
    {
        var calls = new List<string>();
        var prefix = Given(() => calls.Add("given"))
            .And(() => calls.Add("and given"))
            .When(() => calls.Add("when"));
        var first = prefix.Then(() => calls.Add("first"));
        var second = prefix.Then(() => calls.Add("second"));

        Assert.That(BDTestUtil.GetNotRunScenarios(), Does.Contain(first).And.Contain(second));
        Assert.That(BDTestUtil.GetNotRunScenarios(), Does.Not.Contain(prefix));

        var firstResult = await first.BDTestAsync();
        Assert.That(calls, Is.EqualTo(new[] { "given", "and given", "when", "first" }));
        Assert.That(BDTestUtil.GetNotRunScenarios(), Does.Contain(second).And.Not.Contain(first));
        calls.Clear();
        var secondResult = await second.BDTestAsync();

        Assert.That(calls, Is.EqualTo(new[] { "given", "and given", "when", "second" }));
        Assert.That(firstResult.Guid, Is.Not.EqualTo(secondResult.Guid));
        Assert.That(BDTestUtil.GetScenarios(), Does.Contain(firstResult).And.Contain(secondResult));
        Assert.That(BDTestUtil.GetNotRunScenarios(), Does.Not.Contain(first).And.Not.Contain(second));
        Assert.That(firstResult.Steps.All(step => step.ScenarioId == firstResult.Guid), Is.True);
        Assert.That(secondResult.Steps.All(step => step.ScenarioId == secondResult.Guid), Is.True);
        Assert.That(firstResult.Steps.Select(step => step.Guid)
            .Intersect(secondResult.Steps.Select(step => step.Guid)), Is.Empty);
        Assert.That(first.ReportId, Is.EqualTo(prefix.ReportId));
        Assert.That(secondResult.Steps.All(step => step.ReportId == prefix.ReportId), Is.True);
    }

    [Test]
    public async Task Text_overrides_stay_on_the_builder_that_received_them()
    {
        var prefix = When(() => NoOp()).WithStepText(() => "original prefix");
        var first = prefix.Then(() => NoOp()).WithStepText(() => "original first");
        var second = prefix.Then(() => NoOp()).WithStepText(() => "second");
        var extended = first.And(() => NoOp()).WithStepText(() => "extension");

        prefix.WithStepText(() => "changed prefix");
        _ = first.WithStepText(() => "changed first");

        var firstResult = await first.BDTestAsync();
        var secondResult = await second.BDTestAsync();
        var extendedResult = await extended.BDTestAsync();
        Assert.That(firstResult.Steps.Select(step => step.StepText),
            Is.EqualTo(new[] { "When original prefix", "Then changed first" }));
        Assert.That(secondResult.Steps.Select(step => step.StepText),
            Is.EqualTo(new[] { "When original prefix", "Then second" }));
        Assert.That(extendedResult.Steps.Select(step => step.StepText),
            Is.EqualTo(new[] { "When original prefix", "Then original first", "And extension" }));
    }

    [Test]
    public async Task A_failed_branch_does_not_mutate_a_completed_siblings_step_data()
    {
        var invocation = new int[1];
        var prefix = When(() => WriteAndFailOnSecondInvocation(invocation));
        var first = prefix.Then(() => NoOp()).WithScenarioText("successful branch");
        var second = prefix.Then(() => NoOp()).WithScenarioText("failed branch");

        var firstResult = await first.BDTestAsync();
        var firstStep = firstResult.Steps[0];
        var startTime = firstStep.StartTime;
        var endTime = firstStep.EndTime;
        var timeTaken = firstStep.TimeTaken;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await second.BDTestAsync());
        var secondResult = BDTestUtil.GetScenarios().Single(scenario =>
            scenario.FrameworkTestId == firstResult.FrameworkTestId && scenario.GetScenarioText() == "failed branch");

        Assert.That(firstResult.Status, Is.EqualTo(Status.Passed));
        Assert.That(firstStep.Status, Is.EqualTo(Status.Passed));
        Assert.That(firstStep.Exception, Is.Null);
        Assert.That(firstStep.Output, Is.EqualTo("invocation 1"));
        Assert.That(firstStep.StartTime, Is.EqualTo(startTime));
        Assert.That(firstStep.EndTime, Is.EqualTo(endTime));
        Assert.That(firstStep.TimeTaken, Is.EqualTo(timeTaken));
        Assert.That(firstStep.IsRetry, Is.False);
        Assert.That(secondResult.Steps[0], Is.Not.SameAs(firstStep));
        Assert.That(secondResult.Status, Is.EqualTo(Status.Failed));
        Assert.That(secondResult.Steps[0].Exception, Is.Not.Null);
        Assert.That(secondResult.Steps[0].Output, Does.Contain("invocation 2").And.Not.Contain("invocation 1"));
        Assert.That(secondResult.Steps[1].Status, Is.EqualTo(Status.NotRun));
    }

    [Test]
    public async Task Extending_an_executed_chain_starts_with_fresh_step_state()
    {
        var invocation = new[] { 1 };
        var original = When(() => WriteAndFailOnSecondInvocation(invocation)).Then(() => NoOp());
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await original.BDTestAsync());
        var extended = original.And(() => NoOp());
        var result = await extended.BDTestAsync();

        Assert.That(result.Status, Is.EqualTo(Status.Passed));
        Assert.That(result.Steps.All(step => step.Exception == null && !step.IsRetry), Is.True);
        Assert.That(result.Steps[0].Output, Is.EqualTo("invocation 3"));
    }

    [Test]
    [BDTestRetry(1)]
    public async Task Retrying_a_branch_does_not_reset_its_siblings_steps()
    {
        var invocation = new int[1];
        var prefix = When(() => WriteAndFailOnSecondInvocation(invocation));
        var first = prefix.Then(() => NoOp());
        var second = prefix.Then(() => NoOp());

        var firstResult = await first.BDTestAsync();
        var secondResult = await second.BDTestAsync();

        Assert.That(firstResult.RetryCount, Is.Zero);
        Assert.That(firstResult.Steps.All(step => !step.IsRetry), Is.True);
        Assert.That(firstResult.Steps[0].Output, Is.EqualTo("invocation 1"));
        Assert.That(secondResult.RetryCount, Is.EqualTo(1));
        Assert.That(secondResult.Status, Is.EqualTo(Status.Passed));
        Assert.That(secondResult.Steps.All(step => step.IsRetry), Is.True);
        Assert.That(secondResult.Steps[0].Output, Is.EqualTo("invocation 3"));
    }

    [Test]
    public async Task Concurrent_branches_capture_their_own_output()
    {
        var rendezvous = new Rendezvous();
        var prefix = Given(() => NoOp());
        var first = prefix.When(() => rendezvous.Run("first")).Then(() => NoOp());
        var second = prefix.When(() => rendezvous.Run("second")).Then(() => NoOp());

        var results = await Task.WhenAll(first.BDTestAsync(), second.BDTestAsync());

        Assert.That(results[0].Steps[1].Output,
            Is.EqualTo($"first start{Environment.NewLine}first end"));
        Assert.That(results[1].Steps[1].Output,
            Is.EqualTo($"second start{Environment.NewLine}second end"));
        Assert.That(results.All(result => result.Status == Status.Passed), Is.True);
    }

    [TestCase("async")]
    [TestCase("await")]
    public async Task Execution_entry_points_select_output_and_teardown_for_the_executing_branch(string mode)
    {
        var prefix = When(() => NoOp());
        var first = prefix.Then(() => Console.WriteLine("first"));
        var second = prefix.Then(() => Console.WriteLine("second"));

        Scenario secondResult;
        Scenario firstResult;
        switch (mode)
        {
            case "async":
                secondResult = await second.BDTestAsync();
                firstResult = await first.BDTestAsync();
                break;
            default:
                secondResult = await second;
                firstResult = await first;
                break;
        }
        Console.WriteLine("teardown");

        Assert.That(firstResult.Steps[1].Output, Is.EqualTo("first"));
        Assert.That(secondResult.Steps[1].Output, Is.EqualTo("second"));
        Assert.That(firstResult.TearDownOutput.Trim(), Is.EqualTo("teardown"));
        Assert.That(secondResult.TearDownOutput, Is.Null.Or.Empty);
    }

    private sealed class Rendezvous
    {
        private readonly TaskCompletionSource _bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public async Task Run(string name)
        {
            Console.WriteLine($"{name} start");
            if (Interlocked.Increment(ref _count) == 2)
            {
                _bothStarted.SetResult();
            }
            await _bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Console.WriteLine($"{name} end");
        }
    }

    private static void NoOp() { }

    private static void WriteAndFailOnSecondInvocation(int[] counter)
    {
        var invocation = ++counter[0];
        Console.WriteLine($"invocation {invocation}");
        if (invocation == 2)
        {
            throw new InvalidOperationException("second invocation failed");
        }
    }
}
