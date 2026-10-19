using System.Reflection;
using BDTest.Attributes;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

public class RetryPolicyTests
{
    [Test]
    public async Task ThrowingRetryPredicatePreservesOriginalFailure()
    {
        using var fixture = new Harness();
        var failure = new InvalidOperationException("original failure");
        fixture.Options.RetryTestRules.Add(_ => true, 5);
        fixture.Options.RetryTestRules.Add(_ => throw new ArgumentException("broken retry predicate"), 5);
        fixture.Start();
        var thrown = await Capture(() => fixture.Run(() => throw failure));
        var scenario = BDTestUtil.GetScenarios(fixture.TestRun).Single();

        await Assert.That(ReferenceEquals(thrown, failure)).IsTrue();
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Exception).Message).IsEqualTo(failure.Message);
        await Assert.That(scenario.Attempts).Count().IsEqualTo(1);
        await Assert.That(Required(Required(scenario.Attempts)[0].Exception).Message).IsEqualTo(failure.Message);
        await Assert.That(scenario.Output).Contains("Retry predicate failed");
        await Assert.That(scenario.Output).Contains("broken retry predicate");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ScenarioSkipPredicateRunsOnceBeforeExecution(bool throwsOnSecondCall)
    {
        using var fixture = new Harness();
        var calls = 0;
        var executions = 0;
        fixture.Options.SkipTestRules.Add(_ => ++calls > 1
            ? throwsOnSecondCall ? throw new ArgumentException("must not run again") : true
            : false);
        fixture.Options.RetryTestRules.Add(_ => true, 2);
        fixture.Start();
        var scenario = await fixture.Run(() =>
        {
            if (++executions < 3) throw new InvalidOperationException("retry");
        });

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(executions).IsEqualTo(3);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.Attempts).Count().IsEqualTo(3);
    }

    [Test]
    public async Task ThrowingScenarioSkipPredicateFailsWithoutRetryOrSteps()
    {
        using var fixture = new Harness();
        var failure = new ArgumentException("broken skip predicate");
        var calls = 0;
        var executions = 0;
        var retryCalls = 0;
        fixture.Options.SkipTestRules.Add(_ => { calls++; throw failure; });
        fixture.Options.RetryTestRules.Add(_ => { retryCalls++; return true; }, 2);
        // A configuration callback failure must not masquerade as a successful test.
        fixture.Options.SuccessExceptionTypes.Add(typeof(ArgumentException));
        fixture.Start();
        var thrown = await Capture(() => fixture.Run(() => executions++));
        var scenario = BDTestUtil.GetScenarios(fixture.TestRun).Single();

        await Assert.That(ReferenceEquals(thrown, failure)).IsTrue();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(executions).IsZero();
        await Assert.That(retryCalls).IsZero();
        await Assert.That(fixture.BeforeCalls).IsZero();
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Exception).Message).IsEqualTo(failure.Message);
        await Assert.That(scenario.Attempts).Count().IsEqualTo(1);
        await Assert.That(scenario.Output).Contains("Scenario skip predicate failed");
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(true, 1)]
    public async Task ExactPrivateOverloadSuppliesRetryPolicyAcrossAwait(bool useIntOverload, int expectedRetries)
    {
        using var fixture = new Harness();
        fixture.Options.RetryTestRules.Add(_ => throw new Exception("attribute replaces rules"), 5);
        fixture.Start(typeof(Harness).GetMethod("Annotated", BindingFlags.Instance | BindingFlags.NonPublic,
            null, useIntOverload ? [typeof(int)] : Type.EmptyTypes, null));
        var executions = 0;
        var thrown = await Capture(() => fixture.Run(() => { executions++; throw new InvalidOperationException("retry"); }));
        await Assert.That(thrown is InvalidOperationException).IsTrue();

        await Assert.That(executions).IsEqualTo(expectedRetries + 1);
    }

    [Test]
    public async Task RunDefaultsKeepExceptionFiltersAndLargestMatchingLimit()
    {
        using var fixture = new Harness();
        fixture.Options.RetryTestRules.Add(e => e is InvalidOperationException, 1);
        fixture.Options.RetryTestRules.Add(e => e is InvalidOperationException, 2);
        fixture.Options.RetryTestRules.Add(e => e is ArgumentException, 5);
        fixture.Start();
        var executions = 0;
        var thrown = await Capture(() => fixture.Run(() => { executions++; throw new InvalidOperationException("retry"); }));
        await Assert.That(thrown is InvalidOperationException).IsTrue();
        await Assert.That(executions).IsEqualTo(3);
    }

    private static async Task<Exception?> Capture(Func<Task<Scenario>> execute)
    {
        try { await execute(); return null; }
        catch (Exception exception) { return exception; }
    }

    private sealed class Harness : RunTestBase
    {
        public int BeforeCalls { get; private set; }
        public override Task OnBeforeScenario(ScenarioHookContext context)
        {
            BeforeCalls++;
            return Task.CompletedTask;
        }
        public void Start(MethodInfo? method = null) => BeginTest(Guid.NewGuid().ToString("N"), method);
        public async Task<Scenario> Run(Action step)
        {
            await Task.Yield();
            return await When(step).Then(() => { }).BDTestAsync();
        }

        [BDTestRetry(0)]
        private async Task Annotated() => await Task.Yield();
        [BDTestRetry(1)]
        private async Task Annotated(int value) => await Task.Yield();
    }
}

