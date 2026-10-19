using BDTest.Settings;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class FrameworkOutcomeMapTests : TestRunOwner
{
    [Test]
    public async Task Maps_are_immutable_and_registration_is_idempotent()
    {
        var original = new FrameworkOutcomeMap(_ => { }).Skip<ArgumentException>();
        var changed = original.Success<InvalidOperationException>().Inconclusive<ApplicationException>();
        var first = new BDTestOptions();
        first.RegisterFrameworkOutcomes(original);
        first.RegisterFrameworkOutcomes(original.Skip<ArgumentException>());
        var second = new BDTestOptions();
        second.RegisterFrameworkOutcomes(changed);
        await Assert.That(first.SkipExceptionTypes).IsEquivalentTo(new[] { typeof(ArgumentException) }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(first.SuccessExceptionTypes).IsEmpty();
        await Assert.That(first.InconclusiveExceptionTypes).IsEmpty();
        await Assert.That(second.SuccessExceptionTypes).IsEquivalentTo(new[] { typeof(InvalidOperationException) }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(second.InconclusiveExceptionTypes).IsEquivalentTo(new[] { typeof(ApplicationException) }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Inconclusive_notification_falls_back_to_skip_only_when_unspecified()
    {
        var calls = new List<string>();
        var fallback = new FrameworkOutcomeMap(reason => calls.Add("skip " + reason));
        fallback.ReportInconclusive("fallback");
        var distinct = new FrameworkOutcomeMap(reason => calls.Add("skip " + reason),
            reason => calls.Add("inconclusive " + reason));
        distinct.ReportInconclusive("distinct");
        await Assert.That(calls).IsEquivalentTo(new[] { "skip fallback", "inconclusive distinct" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Run_freezes_its_own_notifications_and_the_last_map_supplies_notifications()
    {
        var calls = new List<string>();
        var options = new BDTestOptions();
        options.SkipTestRules.Add(_ => true);
        options.RegisterFrameworkOutcomes(new FrameworkOutcomeMap(reason => calls.Add("first")));
        using var first = BDTestRun.Start(options);
        options.RegisterFrameworkOutcomes(new FrameworkOutcomeMap(reason => calls.Add("second")));
        using var second = BDTestRun.Start(options);
        await new Capture(first).When(() => MustNotRun()).Then(() => MustNotRun()).BDTestAsync();
        await new Capture(second).When(() => MustNotRun()).Then(() => MustNotRun()).BDTestAsync();
        await Assert.That(calls).IsEquivalentTo(new[] { "first", "second" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(first.Scenarios.Single().Status).IsEqualTo(Status.Skipped);
        await Assert.That(second.Scenarios.Single().Status).IsEqualTo(Status.Skipped);
    }

    [Test]
    public async Task Null_maps_and_null_skip_callbacks_are_rejected()
    {
        await Assert.That(() => new FrameworkOutcomeMap(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new BDTestOptions().RegisterFrameworkOutcomes(null!)).ThrowsExactly<ArgumentNullException>();
    }

    private static void MustNotRun() => throw new InvalidOperationException("Skipped scenario executed");
    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }
}
