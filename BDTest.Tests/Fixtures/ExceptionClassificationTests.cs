using System.Reflection;
using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Tests.Helpers;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ExceptionClassificationTests : BDTestBase
{
    private BDTestOptions _options = null!; // Assigned by NUnit SetUp.
    private BDTestRun? _run;
    public override BDTestRun TestRun => _run ??= BDTestRun.Start(_options);

    [SetUp]
    public void ConfigureRun() => _options = new BDTestOptions();

    [TearDown]
    public void DisposeRun()
    {
        EndTest();
        _run?.Dispose();
        _run = null;
    }

    [Test]
    public async Task Steps_and_scenarios_share_classification(
        [Values(Status.NotImplemented, Status.Passed, Status.Skipped, Status.Inconclusive, Status.Failed)] Status status,
        [Values] bool subclass, [Values] Wrapper wrapper)
    {
        _options.SuccessExceptionTypes.Add(typeof(SuccessSignal));
        _options.SkipExceptionTypes.Add(typeof(SkipSignal));
        _options.InconclusiveExceptionTypes.Add(typeof(InconclusiveSignal));
        var exception = Wrap(CreateException(status, subclass), wrapper);
        var builder = Given(() => NoOp()).When(() => Throw(exception)).Then(() => NoOp());

        var thrown = await Capture(builder.BDTestAsync);

        Assert.That(thrown, Is.SameAs(exception), "Classification must preserve the original thrown exception.");
        AssertScenario(TestRun.Scenarios.Single(), status);
    }

    [Test]
    public async Task Outline_rows_share_classification(
        [Values(Status.NotImplemented, Status.Passed, Status.Skipped, Status.Inconclusive, Status.Failed)] Status status,
        [Values] Wrapper wrapper, [Values] bool duringConstruction)
    {
        _options.SuccessExceptionTypes.Add(typeof(SuccessSignal));
        _options.SkipExceptionTypes.Add(typeof(SkipSignal));
        _options.InconclusiveExceptionTypes.Add(typeof(InconclusiveSignal));
        var exception = Wrap(CreateException(status, subclass: true), wrapper);
        var builder = ScenarioOutline("Exception outcomes", new[] { new { Value = 1 } }, _ =>
        {
            if (duringConstruction) throw exception;
            return Given(() => NoOp()).When(() => Throw(exception)).Then(() => NoOp());
        });

        var thrown = await Capture(builder.BDTestAsync);

        Assert.That(thrown, status == Status.Passed ? Is.Null : Is.SameAs(exception));
        var outline = TestRun.Scenarios.Single();
        Assert.That(outline.Status, Is.EqualTo(status));
        var row = Required(outline.Examples).Single();
        if (duringConstruction) Assert.That(row.Status, Is.EqualTo(status));
        else AssertScenario(row, status);
    }

    [Test]
    public async Task Empty_and_multi_inner_aggregates_do_not_hide_failures([Values] bool empty, [Values] bool nested)
    {
        _options.SkipExceptionTypes.Add(typeof(SkipSignal));
        Exception exception = empty
            ? new AggregateException()
            : new AggregateException(new DerivedSkipSignal(), new FailureSignal());
        if (nested) exception = Wrap(exception, Wrapper.Nested);
        var builder = When(() => Throw(exception)).Then(() => NoOp());

        Assert.That(await Capture(builder.BDTestAsync), Is.SameAs(exception));
        var scenario = TestRun.Scenarios.Single();
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Failed));
    }

    [Test]
    public async Task Empty_and_multi_inner_aggregates_can_be_explicitly_registered([Values] bool empty)
    {
        _options.SkipExceptionTypes.Add(typeof(AggregateException));
        var exception = empty
            ? new AggregateException()
            : new AggregateException(new FailureSignal(), new FailureSignal());
        var builder = When(() => Throw(exception)).Then(() => NoOp());

        Assert.That(await Capture(builder.BDTestAsync), Is.SameAs(exception));
        var scenario = TestRun.Scenarios.Single();
        Assert.That(scenario.Status, Is.EqualTo(Status.Skipped));
        Assert.That(Required(scenario.Steps).All(step => step.Status == Status.Skipped), Is.True);
    }

    [TestCase(Status.NotImplemented)]
    [TestCase(Status.Passed)]
    [TestCase(Status.Skipped)]
    [TestCase(Status.Inconclusive)]
    [TestCase(Status.Failed)]
    [BDTestRetry(1)]
    public async Task Only_failed_outcomes_retry(Status status)
    {
        _options.SuccessExceptionTypes.Add(typeof(SuccessSignal));
        _options.SkipExceptionTypes.Add(typeof(SkipSignal));
        _options.InconclusiveExceptionTypes.Add(typeof(InconclusiveSignal));
        BeginTest(TestContext.CurrentContext.Test.ID, Required(global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Method).MethodInfo);
        var exception = Wrap(CreateException(status, subclass: true), Wrapper.Nested);
        var builder = When(() => Throw(exception)).Then(() => NoOp());

        await Capture(builder.BDTestAsync);

        var scenario = TestRun.Scenarios.Single();
        Assert.That(scenario.Status, Is.EqualTo(status));
        Assert.That(scenario.RetryCount, Is.EqualTo(status == Status.Failed ? 1 : 0));
    }

    [TestCase(Status.NotImplemented)]
    [TestCase(Status.Passed)]
    [TestCase(Status.Skipped)]
    public async Task Classification_preserves_precedence(Status expected)
    {
        _options.SuccessExceptionTypes.Add(expected == Status.Skipped ? typeof(SuccessSignal) : typeof(Exception));
        _options.SkipExceptionTypes.Add(typeof(Exception));
        _options.InconclusiveExceptionTypes.Add(typeof(Exception));
        var exception = Wrap(expected == Status.NotImplemented ? new DerivedNotImplemented() : new FailureSignal(), Wrapper.Nested);
        var builder = Given(() => NoOp()).When(() => Throw(exception)).Then(() => NoOp());

        await Capture(builder.BDTestAsync);

        AssertScenario(TestRun.Scenarios.Single(), expected);
    }

    [TestCase(Status.Passed)]
    [TestCase(Status.Skipped)]
    [TestCase(Status.Inconclusive)]
    public async Task Timeouts_always_fail_even_when_registered(Status registeredStatus)
    {
        var settings = _options;
        var types = registeredStatus switch
        {
            Status.Passed => settings.SuccessExceptionTypes,
            Status.Skipped => settings.SkipExceptionTypes,
            _ => settings.InconclusiveExceptionTypes
        };
        types.Add(typeof(TimeoutException));
        var builder = When(() => NoOp()).Then(() => NoOp()).WithScenarioTimeout(TimeSpan.FromTicks(1));

        Assert.That(await Capture(builder.BDTestAsync), Is.TypeOf<TimeoutException>());

        var scenario = TestRun.Scenarios.Single();
        Assert.That(scenario.TimedOut, Is.True);
        Assert.That(scenario.Status, Is.EqualTo(Status.Failed));
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Failed));
    }

    [TestCase(Status.Passed)]
    [TestCase(Status.Skipped)]
    [TestCase(Status.Inconclusive)]
    public async Task Legacy_registrations_remain_live_and_do_not_leak_into_explicit_runs(Status status)
    {
        var exception = Wrap(new DerivedFailureSignal(), Wrapper.Nested);
        var explicitBuilder = When(() => Throw(exception)).Then(() => NoOp());
        var legacyBuilder = new LegacyFixture().When(() => Throw(exception)).Then(() => NoOp());
        var settings = BDTestSettings.CustomExceptionSettings;
        var types = status switch
        {
            Status.Passed => settings.SuccessExceptionTypes,
            Status.Skipped => settings.SkipExceptionTypes,
            _ => settings.InconclusiveExceptionTypes
        };
        using var registration = new ExceptionRegistration(types, typeof(FailureSignal));
        try
        {
            Assert.That(await Capture(explicitBuilder.BDTestAsync), Is.SameAs(exception));
            Assert.That(await Capture(legacyBuilder.BDTestAsync), Is.SameAs(exception));
            Assert.That(TestRun.Scenarios.Single().Status, Is.EqualTo(Status.Failed));
            var legacy = BDTestUtil.GetScenarios().Single(scenario => scenario.Guid == legacyBuilder.Guid);
            Assert.That(legacy.Status, Is.EqualTo(status));
            Assert.That(Required(legacy.Steps)[0].Status, Is.EqualTo(status));
        }
        finally
        {
            BDTestUtil.ClearScenarios();
        }
    }

    private sealed class LegacyFixture : BDTestBase { }

    private static void AssertScenario(Scenario scenario, Status status)
    {
        Assert.That(scenario.Status, Is.EqualTo(status));
        Assert.That(Required(scenario.Attempts).Single().Status, Is.EqualTo(status));
        Assert.That(Required(scenario.Steps)[0].Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.Steps[1].Status, Is.EqualTo(status));
        Assert.That(scenario.Steps[1].Exception, status is Status.Failed or Status.NotImplemented ? Is.Not.Null : Is.Null);
        Assert.That(scenario.Steps[2].Status, Is.EqualTo(status == Status.Skipped ? Status.Skipped : Status.NotRun));
    }

    private static async Task<Exception?> Capture(Func<Task<Scenario>> execute)
    {
        try { await execute(); }
        catch (Exception exception) { return exception; }
        return null;
    }

    private static Exception CreateException(Status status, bool subclass) => status switch
    {
        Status.NotImplemented => subclass ? new DerivedNotImplemented() : new NotImplementedException(),
        Status.Passed => subclass ? new DerivedSuccessSignal() : new SuccessSignal(),
        Status.Skipped => subclass ? new DerivedSkipSignal() : new SkipSignal(),
        Status.Inconclusive => subclass ? new DerivedInconclusiveSignal() : new InconclusiveSignal(),
        _ => subclass ? new DerivedFailureSignal() : new FailureSignal()
    };

    private static Exception Wrap(Exception exception, Wrapper wrapper) => wrapper switch
    {
        Wrapper.Reflection => new TargetInvocationException(exception),
        Wrapper.Aggregate => new AggregateException(exception),
        Wrapper.Nested => new TargetInvocationException(new AggregateException(new TargetInvocationException(new AggregateException(exception)))),
        _ => exception
    };

    private static void Throw(Exception exception) => throw exception;
    private static void NoOp() { }
    public enum Wrapper { None, Reflection, Aggregate, Nested }
    private class SuccessSignal : Exception { }
    private class DerivedSuccessSignal : SuccessSignal { }
    private class SkipSignal : Exception { }
    private class DerivedSkipSignal : SkipSignal { }
    private class InconclusiveSignal : Exception { }
    private class DerivedInconclusiveSignal : InconclusiveSignal { }
    private class FailureSignal : Exception { }
    private class DerivedFailureSignal : FailureSignal { }
    private class DerivedNotImplemented : NotImplementedException { }
}
