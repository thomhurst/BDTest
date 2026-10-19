using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ScenarioHookTests
{
    [TestCase("before")]
    [TestCase("after")]
    public async Task NUnit_hook_assertions_are_isolated_for_retries(string failureAt)
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(e => e is AssertionException, 1);
        using var run = BDTestRun.Start(options);
        var after = new List<Status>();
        var fixture = new NUnitFixture(run)
        {
            Before = s =>
            {
                if (failureAt == "before") Assert.That(s.Attempt, Is.EqualTo(1));
                return Task.CompletedTask;
            },
            After = s =>
            {
                after.Add(s.Scenario.Status);
                if (failureAt == "after") Assert.That(s.Attempt, Is.EqualTo(1));
                return Task.CompletedTask;
            }
        };
        var scenario = await fixture.When(() => Complete()).Then(() => Complete()).BDTestAsync();
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(after, Is.EqualTo(new[] { failureAt == "before" ? Status.Failed : Status.Passed, Status.Passed }));
    }


    [Test]
    public async Task NUnit_caught_assertion_is_visible_to_after_scenario_hook()
    {
        using var run = BDTestRun.Start();
        Status? observed = null;
        var fixture = new NUnitFixture(run)
        {
            After = s => { observed = s.Scenario.Status; return Task.CompletedTask; }
        };
        await Assert.ThrowsAsync<AssertionException>(async () => await fixture.When(() => CaughtAssertion())
            .Then(() => Complete()).BDTestAsync());
        Assert.That(observed, Is.EqualTo(Status.Failed));

        static void CaughtAssertion()
        {
            try { Assert.Fail("caught failure"); }
            catch (AssertionException) { }
        }
    }


    [Test]
    public async Task NUnit_cleanup_diagnostics_do_not_replace_original_failure()
    {
        using var run = BDTestRun.Start();
        var failure = new InvalidOperationException("original step failure");
        var fixture = new NUnitFixture(run)
        {
            After = _ =>
            {
                for (var i = 0; i < 2; i++)
                {
                    try { Assert.Fail("secondary cleanup assertion"); }
                    catch (AssertionException) { }
                }
                throw new ApplicationException("secondary cleanup exception");
            }
        };
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture
            .When(() => throw failure).Then(() => Complete()).BDTestAsync());
        Assert.That(thrown, Is.SameAs(failure));
    }


    private sealed class State { public int Attempt { get; set; } }

    private sealed class NUnitFixture(BDTestRun run) : BDTest.NUnit.NUnitBDTestBase<State>
    {
        public override BDTestRun TestRun => run;
        public Func<ScenarioHookContext<State>, Task> Before { get; init; } = _ => Task.CompletedTask;
        public Func<ScenarioHookContext<State>, Task> After { get; init; } = _ => Task.CompletedTask;
        public override Task OnBeforeScenario(ScenarioHookContext<State> scenario) => Before(scenario);
        public override Task OnAfterScenario(ScenarioHookContext<State> scenario) => After(scenario);
    }

    private static void Complete() { }

}
