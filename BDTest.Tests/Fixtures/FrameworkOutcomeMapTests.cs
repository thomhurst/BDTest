using BDTest.Settings;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class FrameworkOutcomeMapTests : TestRunOwner
{
    [Test]
    public void Maps_are_immutable_and_registration_is_idempotent()
    {
        var original = new FrameworkOutcomeMap(_ => { }).Skip<ArgumentException>();
        var changed = original.Success<InvalidOperationException>().Inconclusive<ApplicationException>();
        var first = new BDTestOptions();
        first.RegisterFrameworkOutcomes(original);
        first.RegisterFrameworkOutcomes(original.Skip<ArgumentException>());
        var second = new BDTestOptions();
        second.RegisterFrameworkOutcomes(changed);
        Assert.That(first.SkipExceptionTypes, Is.EqualTo(new[] { typeof(ArgumentException) }));
        Assert.That(first.SuccessExceptionTypes, Is.Empty);
        Assert.That(first.InconclusiveExceptionTypes, Is.Empty);
        Assert.That(second.SuccessExceptionTypes, Is.EqualTo(new[] { typeof(InvalidOperationException) }));
        Assert.That(second.InconclusiveExceptionTypes, Is.EqualTo(new[] { typeof(ApplicationException) }));
    }

    [Test]
    public void Inconclusive_notification_falls_back_to_skip_only_when_unspecified()
    {
        var calls = new List<string>();
        var fallback = new FrameworkOutcomeMap(reason => calls.Add("skip " + reason));
        fallback.ReportInconclusive("fallback");
        var distinct = new FrameworkOutcomeMap(reason => calls.Add("skip " + reason),
            reason => calls.Add("inconclusive " + reason));
        distinct.ReportInconclusive("distinct");
        Assert.That(calls, Is.EqualTo(new[] { "skip fallback", "inconclusive distinct" }));
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
        Assert.That(calls, Is.EqualTo(new[] { "first", "second" }));
        Assert.That(first.Scenarios.Single().Status, Is.EqualTo(Status.Skipped));
        Assert.That(second.Scenarios.Single().Status, Is.EqualTo(Status.Skipped));
    }

    [Test]
    public void Null_maps_and_null_skip_callbacks_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new FrameworkOutcomeMap(null!));
        Assert.Throws<ArgumentNullException>(() => new BDTestOptions().RegisterFrameworkOutcomes(null!));
    }

    private static void MustNotRun() => throw new InvalidOperationException("Skipped scenario executed");
    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
    }
}
