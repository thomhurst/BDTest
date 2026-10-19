using BDTest.Attributes;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class StepBuilderBranchTests : RunTestBase
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    [Test]
    public async Task Branches_execute_only_their_own_steps_and_are_reported_separately()
    {
        var calls = new List<string>();
        var prefix = Given(() => calls.Add("given"))
            .And(() => calls.Add("and given"))
            .When(() => calls.Add("when"));
        var first = prefix.Then(() => calls.Add("first"));
        var second = prefix.Then(() => calls.Add("second"));


        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun).Select(item => item.ScenarioId)).Contains(Required(first.Guid)).And.Contains(Required(second.Guid));
        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun).Select(item => item.ScenarioId)).DoesNotContain(Required(prefix.Guid));

        var firstResult = await first.BDTestAsync();
        await Assert.That(calls).IsEquivalentTo(new[] { "given", "and given", "when", "first" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun).Select(item => item.ScenarioId)).Contains(Required(second.Guid)).And.DoesNotContain(Required(first.Guid));
        calls.Clear();
        var secondResult = await second.BDTestAsync();

        await Assert.That(calls).IsEquivalentTo(new[] { "given", "and given", "when", "second" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(firstResult.Guid).IsNotEqualTo(secondResult.Guid);

        await Assert.That(BDTestUtil.GetScenarios(TestRun).Select(s => s.Guid)).Contains(firstResult.Guid).And.Contains(secondResult.Guid);

        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun).Select(item => item.ScenarioId)).DoesNotContain(Required(first.Guid)).And.DoesNotContain(Required(second.Guid));
        await Assert.That(Required(firstResult.Steps).All(step => step.ScenarioId == firstResult.Guid)).IsTrue();
        await Assert.That(Required(secondResult.Steps).All(step => step.ScenarioId == secondResult.Guid)).IsTrue();
        await Assert.That(firstResult.Steps.Select(step => step.Guid)
            .Intersect(secondResult.Steps.Select(step => step.Guid))).IsEmpty();
        await Assert.That(first.ReportId).IsEqualTo(prefix.ReportId);
        await Assert.That(secondResult.Steps.All(step => step.ReportId == prefix.ReportId)).IsTrue();
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
        await Assert.That(Required(firstResult.Steps).Select(step => step.StepText)).IsEquivalentTo((new[] { "When original prefix", "Then changed first" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(secondResult.Steps).Select(step => step.StepText)).IsEquivalentTo((new[] { "When original prefix", "Then second" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(extendedResult.Steps).Select(step => step.StepText)).IsEquivalentTo((new[] { "When original prefix", "Then original first", "And extension" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_failed_branch_does_not_mutate_a_completed_siblings_step_data()
    {
        var invocation = new int[1];
        var prefix = When(() => WriteAndFailOnSecondInvocation(invocation));
        var first = prefix.Then(() => NoOp()).WithScenarioText("successful branch");
        var second = prefix.Then(() => NoOp()).WithScenarioText("failed branch");

        var firstResult = await first.BDTestAsync();
        var firstStep = Required(firstResult.Steps)[0];
        var startTime = firstStep.StartTime;
        var endTime = firstStep.EndTime;
        var timeTaken = firstStep.TimeTaken;
        await Assert.That(async () => await second.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var secondResult = BDTestUtil.GetScenarios(TestRun).Single(scenario =>
            scenario.FrameworkTestId == firstResult.FrameworkTestId && scenario.ScenarioText == "failed branch");

        await Assert.That(firstResult.Status).IsEqualTo(Status.Passed);
        await Assert.That(firstStep.Status).IsEqualTo(Status.Passed);
        await Assert.That(firstStep.Exception).IsNull();
        await Assert.That(firstStep.Output).IsEqualTo("invocation 1");
        await Assert.That(firstStep.StartTime).IsEqualTo(startTime);
        await Assert.That(firstStep.EndTime).IsEqualTo(endTime);
        await Assert.That(firstStep.TimeTaken).IsEqualTo(timeTaken);
        await Assert.That(firstStep.IsRetry).IsFalse();
        await Assert.That(Required(secondResult.Steps)[0]).IsNotSameReferenceAs(firstStep);
        await Assert.That(secondResult.Status).IsEqualTo(Status.Failed);
        await Assert.That(secondResult.Steps[0].Exception).IsNotNull();

        await Assert.That(secondResult.Steps[0].Output).Contains("invocation 2").And.DoesNotContain("invocation 1");
        await Assert.That(secondResult.Steps[1].Status).IsEqualTo(Status.NotRun);
    }

    [Test]
    public async Task Extending_an_executed_chain_starts_with_fresh_step_state()
    {
        var invocation = new[] { 1 };
        var original = When(() => WriteAndFailOnSecondInvocation(invocation)).Then(() => NoOp());
        await Assert.That(async () =>
            await original.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var extended = original.And(() => NoOp());
        var result = await extended.BDTestAsync();

        await Assert.That(result.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(result.Steps).All(step => step.Exception == null && !step.IsRetry)).IsTrue();
        await Assert.That(result.Steps[0].Output).IsEqualTo("invocation 3");
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

        await Assert.That(firstResult.RetryCount).IsZero();
        await Assert.That(Required(firstResult.Steps).All(step => !step.IsRetry)).IsTrue();
        await Assert.That(firstResult.Steps[0].Output).IsEqualTo("invocation 1");
        await Assert.That(secondResult.RetryCount).IsEqualTo(1);
        await Assert.That(secondResult.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(secondResult.Steps).All(step => step.IsRetry)).IsTrue();
        await Assert.That(secondResult.Steps[0].Output).IsEqualTo("invocation 3");
    }

    [Test]
    public async Task Concurrent_branches_capture_their_own_output()
    {
        var rendezvous = new Rendezvous();
        var prefix = Given(() => NoOp());
        var first = prefix.When(() => rendezvous.Run("first")).Then(() => NoOp());
        var second = prefix.When(() => rendezvous.Run("second")).Then(() => NoOp());

        var results = await Task.WhenAll(first.BDTestAsync(), second.BDTestAsync());

        await Assert.That(Required(results[0].Steps)[1].Output).IsEqualTo($"first start{Environment.NewLine}first end");
        await Assert.That(Required(results[1].Steps)[1].Output).IsEqualTo($"second start{Environment.NewLine}second end");
        await Assert.That(results.All(result => result.Status == Status.Passed)).IsTrue();
    }

    [Test]
    [Arguments("async")]
    [Arguments("await")]
    public async Task Execution_entry_points_select_output_and_teardown_for_the_executing_branch(string mode)
    {
        var prefix = When(() => NoOp());
        var first = prefix.Then(() => Console.WriteLine("first"));
        var second = prefix.Then(() => Console.WriteLine("second"));

        BDTest.Contracts.ScenarioResult secondResult;
        BDTest.Contracts.ScenarioResult firstResult;
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

        await Assert.That(Required(firstResult.Steps)[1].Output).IsEqualTo("first");
        await Assert.That(Required(secondResult.Steps)[1].Output).IsEqualTo("second");
        await Assert.That(Required(BDTestUtil.GetScenarios(TestRun).Single(s => s.Guid == firstResult.Guid).TearDownOutput).Trim()).IsEqualTo("teardown");

        await Assert.That(secondResult.TearDownOutput).IsNullOrEmpty();
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
