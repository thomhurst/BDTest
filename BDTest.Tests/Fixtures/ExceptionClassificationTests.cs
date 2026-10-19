using System.Reflection;
using BDTest;
using BDTest.Settings;
using BDTest.Tests.Helpers;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class ExceptionClassificationTests : BDTestBase
{
    private BDTestOptions _options = null!; // Assigned by the per-test setup hook.
    private BDTestRun? _run;
    public override BDTestRun TestRun => _run ??= BDTestRun.Start(_options);

    [Before(HookType.Test)]
    public void ConfigureRun() => _options = new BDTestOptions();

    [After(HookType.Test)]
    public void DisposeRun()
    {
        EndTest();
        _run?.Dispose();
        _run = null;
    }

    [Test]
    [MatrixDataSource]
    public async Task Steps_and_scenarios_share_classification(
        [Matrix(Status.NotImplemented, Status.Passed, Status.Skipped, Status.Inconclusive, Status.Failed)] Status status,
        [Matrix] bool subclass, [Matrix] Wrapper wrapper)
    {
        _options.SuccessExceptionTypes.Add(typeof(SuccessSignal));
        _options.SkipExceptionTypes.Add(typeof(SkipSignal));
        _options.InconclusiveExceptionTypes.Add(typeof(InconclusiveSignal));
        var exception = Wrap(CreateException(status, subclass), wrapper);
        var builder = Given(() => NoOp()).When(() => Throw(exception)).Then(() => NoOp());

        var thrown = await Capture(builder.BDTestAsync);

        await Assert.That(thrown).IsSameReferenceAs(exception).Because("Classification must preserve the original thrown exception.");
        await AssertScenario(TestRun.Scenarios.Single(), status);
    }

    [Test]
    [MatrixDataSource]
    public async Task Outline_rows_share_classification(
        [Matrix(Status.NotImplemented, Status.Passed, Status.Skipped, Status.Inconclusive, Status.Failed)] Status status,
        [Matrix] Wrapper wrapper, [Matrix] bool duringConstruction)
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


        await Assert.That(thrown).IsSameReferenceAs(status == Status.Passed ? null : exception);
        var outline = TestRun.Scenarios.Single();
        await Assert.That(outline.Status).IsEqualTo(status);
        var row = Required(outline.Examples).Single();
        if (duringConstruction) await Assert.That(row.Status).IsEqualTo(status);
        else await AssertScenario(row, status);
    }

    [Test]
    [MatrixDataSource]
    public async Task Empty_and_multi_inner_aggregates_do_not_hide_failures([Matrix] bool empty, [Matrix] bool nested)
    {
        _options.SkipExceptionTypes.Add(typeof(SkipSignal));
        Exception exception = empty
            ? new AggregateException()
            : new AggregateException(new DerivedSkipSignal(), new FailureSignal());
        if (nested) exception = Wrap(exception, Wrapper.Nested);
        var builder = When(() => Throw(exception)).Then(() => NoOp());

        await Assert.That(await Capture(builder.BDTestAsync)).IsSameReferenceAs(exception);
        var scenario = TestRun.Scenarios.Single();
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Failed);
    }

    [Test]
    [MatrixDataSource]
    public async Task Empty_and_multi_inner_aggregates_can_be_explicitly_registered([Matrix] bool empty)
    {
        _options.SkipExceptionTypes.Add(typeof(AggregateException));
        var exception = empty
            ? new AggregateException()
            : new AggregateException(new FailureSignal(), new FailureSignal());
        var builder = When(() => Throw(exception)).Then(() => NoOp());

        await Assert.That(await Capture(builder.BDTestAsync)).IsSameReferenceAs(exception);
        var scenario = TestRun.Scenarios.Single();
        await Assert.That(scenario.Status).IsEqualTo(Status.Skipped);
        await Assert.That(Required(scenario.Steps).All(step => step.Status == Status.Skipped)).IsTrue();
    }

    [Test]
    [Arguments(Status.NotImplemented)]
    [Arguments(Status.Passed)]
    [Arguments(Status.Skipped)]
    [Arguments(Status.Inconclusive)]
    [Arguments(Status.Failed)]
    [BDTestRetry(1)]
    public async Task Only_failed_outcomes_retry(Status status)
    {
        _options.SuccessExceptionTypes.Add(typeof(SuccessSignal));
        _options.SkipExceptionTypes.Add(typeof(SkipSignal));
        _options.InconclusiveExceptionTypes.Add(typeof(InconclusiveSignal));
        BeginTest(Required(TestContext.Current).Id, Required(TestContext.Current).Metadata.TestDetails.MethodMetadata.GetReflectionInfo());
        var exception = Wrap(CreateException(status, subclass: true), Wrapper.Nested);
        var builder = When(() => Throw(exception)).Then(() => NoOp());

        await Capture(builder.BDTestAsync);

        var scenario = TestRun.Scenarios.Single();
        await Assert.That(scenario.Status).IsEqualTo(status);
        await Assert.That(scenario.RetryCount).IsEqualTo(status == Status.Failed ? 1 : 0);
    }

    [Test]
    [Arguments(Status.NotImplemented)]
    [Arguments(Status.Passed)]
    [Arguments(Status.Skipped)]
    public async Task Classification_preserves_precedence(Status expected)
    {
        _options.SuccessExceptionTypes.Add(expected == Status.Skipped ? typeof(SuccessSignal) : typeof(Exception));
        _options.SkipExceptionTypes.Add(typeof(Exception));
        _options.InconclusiveExceptionTypes.Add(typeof(Exception));
        var exception = Wrap(expected == Status.NotImplemented ? new DerivedNotImplemented() : new FailureSignal(), Wrapper.Nested);
        var builder = Given(() => NoOp()).When(() => Throw(exception)).Then(() => NoOp());

        await Capture(builder.BDTestAsync);

        await AssertScenario(TestRun.Scenarios.Single(), expected);
    }

    [Test]
    [Arguments(Status.Passed)]
    [Arguments(Status.Skipped)]
    [Arguments(Status.Inconclusive)]
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

        await Assert.That(await Capture(builder.BDTestAsync)).IsTypeOf<TimeoutException>();

        var scenario = TestRun.Scenarios.Single();
        await Assert.That(scenario.TimedOut).IsTrue();
        await Assert.That(scenario.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Failed);
    }

    [Test]
    [Arguments(Status.Passed)]
    [Arguments(Status.Skipped)]
    [Arguments(Status.Inconclusive)]
    public async Task Exception_registrations_are_isolated_between_explicit_runs(Status status)
    {
        var exception = Wrap(new DerivedFailureSignal(), Wrapper.Nested);
        var explicitBuilder = When(() => Throw(exception)).Then(() => NoOp());
        var settings = new BDTestOptions();
        var types = status switch
        {
            Status.Passed => settings.SuccessExceptionTypes,
            Status.Skipped => settings.SkipExceptionTypes,
            _ => settings.InconclusiveExceptionTypes
        };
        types.Add(typeof(FailureSignal));
        using var customRun = BDTestRun.Start(settings);
        var configuredBuilder = new ExplicitFixture(customRun).When(() => Throw(exception)).Then(() => NoOp());
        await Assert.That(await Capture(explicitBuilder.BDTestAsync)).IsSameReferenceAs(exception);
        await Assert.That(await Capture(configuredBuilder.BDTestAsync)).IsSameReferenceAs(exception);
        await Assert.That(TestRun.Scenarios.Single().Status).IsEqualTo(Status.Failed);
        var configured = customRun.Scenarios.Single(scenario => scenario.Guid == configuredBuilder.Guid);
        await Assert.That(configured.Status).IsEqualTo(status);
        await Assert.That(Required(configured.Steps)[0].Status).IsEqualTo(status);

    }

    private sealed class ExplicitFixture(BDTestRun run) : BDTestBase { public override BDTestRun TestRun => run; }

    private static async Task AssertScenario(BDTest.Contracts.ScenarioResult scenario, Status status)
    {
        await Assert.That(scenario.Status).IsEqualTo(status);
        await Assert.That(Required(scenario.Attempts).Single().Status).IsEqualTo(status);
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.Steps[1].Status).IsEqualTo(status);

        await Assert.That(scenario.Steps[1].Exception is not null).IsEqualTo(status is Status.Failed or Status.NotImplemented);
        await Assert.That(scenario.Steps[2].Status).IsEqualTo(status == Status.Skipped ? Status.Skipped : Status.NotRun);
    }

    private static async Task<Exception?> Capture(Func<Task<BDTest.Contracts.ScenarioResult>> execute)
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
