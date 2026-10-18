using System.Reflection;
using BDTest.Attributes;
using BDTest.Interfaces;
using BDTest.Test;
using BDTest.Test.Steps.Then;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class BDTestOptionsTests
{
    [Test]
    public async Task Concurrent_runs_classify_step_and_outline_factory_exceptions_independently(
        [Values(Status.Passed, Status.Skipped, Status.Inconclusive)] Status status, [Values] bool wrapped)
    {
        var options = new BDTestOptions();
        var types = status == Status.Passed ? options.SuccessExceptionTypes
            : status == Status.Skipped ? options.SkipExceptionTypes : options.InconclusiveExceptionTypes;
        types.Add(typeof(RegisteredException));
        using var configured = BDTestRun.Start(options);
        using var plain = BDTestRun.Start();
        types.Clear();
        Exception CreateException() => wrapped
            ? new TargetInvocationException(new AggregateException(new DerivedRegisteredException()))
            : new RegisteredException();
        var entered = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> failTogether = async () =>
        {
            if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            throw CreateException();
        };
        async Task Execute(BDTestRun run)
        {
            var fixture = new Capture(run);
            try { await fixture.When(() => failTogether()).Then(() => Complete()).BDTestAsync(); }
            catch (Exception exception) when (exception is RegisteredException or TargetInvocationException) { }
            try
            {
                await fixture.ScenarioOutline("factory", new[] { 1 }, _ => throw CreateException()).BDTestAsync();
            }
            catch (Exception exception) when (exception is RegisteredException or TargetInvocationException) { }
        }
        await Task.WhenAll(Task.Run(() => Execute(configured)), Task.Run(() => Execute(plain)));
        Assert.That(configured.Scenarios.All(scenario => scenario.Status == status), Is.True);
        Assert.That(plain.Scenarios.All(scenario => scenario.Status == Status.Failed), Is.True);
        Assert.That(configured.Scenarios.Single(scenario => !scenario.IsOutline).Steps[0].Status, Is.EqualTo(status));
        Assert.That(configured.Scenarios.Single(scenario => scenario.IsOutline).Examples[0].Status, Is.EqualTo(status));
    }

    [Test]
    public async Task Run_freezes_retry_skip_debug_converter_and_hook_registrations()
    {
        var options = new BDTestOptions();
        var calls = new List<string>();
        options.RetryTestRules.Add(exception => exception is RegisteredException, 1);
        options.SkipStepRules.Add<SkippedAttribute>(_ => true);
        options.RegisterCustomStringConverter(new Converter("frozen"));
        options.StepHooks.Add(new Hook("first", calls));
        options.StepHooks.Add(new Hook("second", calls));
        using var frozen = BDTestRun.Start(options);
        options.RetryTestRules.Add(_ => true, 5);
        options.SkipTestRules.Add(_ => true);
        options.Debug.ShouldSkipWhenStep = true;
        options.StepHooks.Clear();
        options.RegisterCustomStringConverter(new Converter("late"));
        var fixture = new Capture(frozen);
        var steps = new RetrySteps();
        var scenario = await fixture.When(() => steps.FailOnce(new Value()))
            .WithArgs(() => new Value())
            .Then(() => Skipped()).BDTestAsync();
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(steps.Attempts, Is.EqualTo(2));
        Assert.That(scenario.Steps[0].StepText, Does.Contain("frozen").And.Not.Contain("late"));
        Assert.That(scenario.Steps[1].Status, Is.EqualTo(Status.Skipped));
        Assert.That(calls, Is.EqualTo(new[]
        {
            "first before", "second before", "first after Failed", "second after Failed",
            "first before", "second before", "first after Passed", "second after Passed",
            "first before", "second before", "first after Skipped", "second after Skipped"
        }));
        using var late = BDTestRun.Start(options);
        Assert.That((await new Capture(late).When(() => Complete()).Then(() => Complete()).BDTestAsync()).Status,
            Is.EqualTo(Status.Skipped));
    }

    [Test]
    public async Task Debug_and_skip_step_rules_are_isolated_and_frozen()
    {
        var options = new BDTestOptions();
        options.Debug.ShouldSkipWhenStep = true;
        options.SkipStepRules.Add<SkippedAttribute>(_ => true);
        using var skipped = BDTestRun.Start(options);
        using var plain = BDTestRun.Start();
        options.Debug.ShouldSkipWhenStep = false;
        var first = await new Capture(skipped).When(() => Complete()).Then(() => Skipped()).BDTestAsync();
        var second = await new Capture(plain).When(() => Complete()).Then(() => Skipped()).BDTestAsync();
        Assert.That(first.Steps.All(step => step.Status == Status.Skipped), Is.True);
        Assert.That(second.Steps.All(step => step.Status == Status.Passed), Is.True);
    }

    [Test]
    public async Task Typed_converters_preserve_registration_order_and_explicit_interface_implementations()
    {
        var options = new BDTestOptions();
        options.RegisterCustomStringConverter(new Converter("first"));
        options.RegisterCustomStringConverter(new Converter("second"));
        using var first = BDTestRun.Start(options);
        var otherOptions = new BDTestOptions();
        otherOptions.RegisterCustomStringConverter(new Converter("other"));
        using var other = BDTestRun.Start(otherOptions);
        var results = await Task.WhenAll(
            Task.Run(() => new Capture(first).When(() => Keep(new Value())).WithArgs(() => new Value()).Then(() => Complete()).BDTestAsync()),
            Task.Run(() => new Capture(other).When(() => Keep(new Value())).WithArgs(() => new Value()).Then(() => Complete()).BDTestAsync()));
        Assert.That(results[0].Steps[0].StepText, Does.Contain("first").And.Not.Contain("second").And.Not.Contain("other"));
        Assert.That(results[1].Steps[0].StepText, Does.Contain("other").And.Not.Contain("first"));
    }

    [Test]
    public async Task After_hooks_all_run_and_preserve_original_failure()
    {
        var calls = new List<string>();
        var options = new BDTestOptions();
        options.StepHooks.Add(new Hook("failing", calls, failAfter: true));
        options.StepHooks.Add(new Hook("last", calls));
        using var run = BDTestRun.Start(options);
        await Assert.ThrowsAsync<RegisteredException>(async () => await new Capture(run)
            .When(() => Fail()).Then(() => Complete()).BDTestAsync());
        Assert.That(calls, Does.Contain("last after Failed"));
        Assert.That(run.Scenarios.Single().Steps[0].Exception.TypeName, Does.Contain(nameof(RegisteredException)));
    }

    [Test, Combinatorial]
    public async Task Run_converters_apply_to_each_delegate_argument_source(
        [Values("generated", "explicit", "fallback")] string argumentSource,
        [Values("converted", "null", "throws")] string conversion)
    {
        var options = new BDTestOptions();
        options.RegisterCustomStringConverter(new Converter(conversion == "null" ? null : conversion,
            fail: conversion == "throws"));
        using var run = BDTestRun.Start(options);
        var fixture = new Capture(run);
        var builder = fixture.BuildWithArgument(argumentSource);
        Assert.That(fixture.ArgumentReads, Is.Zero);

        var scenario = await builder.BDTestAsync();
        var expected = conversion == "throws" ? "<error: InvalidOperationException>" : conversion;
        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.Steps[0].StepText, Does.EndWith(" " + expected));
        Assert.That(scenario.Steps[0].Diagnostics.Count, Is.EqualTo(conversion == "throws" ? 1 : 0));
    }

    [Test]
    public async Task Retry_limits_and_new_converter_and_skip_rules_do_not_leak_into_existing_runs()
    {
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, 1);
        using var frozen = BDTestRun.Start(options);
        options.RetryTestRules.Add(_ => true, 3);
        options.RegisterCustomStringConverter(new Converter("late converter"));
        options.SkipStepRules.Add<SkippedAttribute>(_ => true);
        using var later = BDTestRun.Start(options);
        async Task Execute(BDTestRun run)
        {
            var fixture = new Capture(run);
            await Assert.ThrowsAsync<RegisteredException>(async () => await fixture.When(() => Fail())
                .Then(() => Complete()).BDTestAsync());
            await fixture.When(() => Keep(new Value())).WithArgs(() => new Value()).Then(() => Skipped()).BDTestAsync();
        }
        await Task.WhenAll(Task.Run(() => Execute(frozen)), Task.Run(() => Execute(later)));
        Assert.That(frozen.Scenarios.Single(scenario => scenario.Status == Status.Failed).RetryCount, Is.EqualTo(1));
        Assert.That(later.Scenarios.Single(scenario => scenario.Status == Status.Failed).RetryCount, Is.EqualTo(3));
        var frozenPassed = frozen.Scenarios.Single(scenario => scenario.Status == Status.Passed);
        var laterPassed = later.Scenarios.Single(scenario => scenario.Status == Status.Passed);
        Assert.That(frozenPassed.Steps[0].StepText, Does.Not.Contain("late converter"));
        Assert.That(laterPassed.Steps[0].StepText, Does.Contain("late converter"));
        Assert.That(frozenPassed.Steps[1].Status, Is.EqualTo(Status.Passed));
        Assert.That(laterPassed.Steps[1].Status, Is.EqualTo(Status.Skipped));
    }

    [Test]
    public async Task Concurrent_runs_execute_only_their_own_hooks()
    {
        var firstCalls = new List<string>();
        var secondCalls = new List<string>();
        var firstOptions = new BDTestOptions();
        var secondOptions = new BDTestOptions();
        firstOptions.StepHooks.Add(new Hook("first", firstCalls));
        secondOptions.StepHooks.Add(new Hook("second", secondCalls));
        using var first = BDTestRun.Start(firstOptions);
        using var second = BDTestRun.Start(secondOptions);
        var entered = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> wait = async () =>
        {
            if (Interlocked.Increment(ref entered) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        };
        await Task.WhenAll(
            Task.Run(() => new Capture(first).When(() => wait()).Then(() => Complete()).BDTestAsync()),
            Task.Run(() => new Capture(second).When(() => wait()).Then(() => Complete()).BDTestAsync()));
        Assert.That(firstCalls, Is.EqualTo(new[] { "first before", "first after Passed", "first before", "first after Passed" }));
        Assert.That(secondCalls, Is.EqualTo(new[] { "second before", "second after Passed", "second before", "second after Passed" }));
    }

    private static void Complete() { }
    private static void Keep(Value value) => GC.KeepAlive(value);
    private static void Fail() => throw new RegisteredException();
    [Skipped] private static void Skipped() { }
    private sealed class SkippedAttribute : SkipStepAttribute { }
    private class RegisteredException : Exception { }
    private sealed class DerivedRegisteredException : RegisteredException { }
    private sealed class Value { }
    private sealed class Converter(string text, bool fail = false) : IStepTextStringConverter<Value>
    {
        string IStepTextStringConverter<Value>.ConvertToString(Value value)
            => fail ? throw new InvalidOperationException("converter failure") : text;
    }
    private sealed class RetrySteps
    {
        public int Attempts { get; private set; }
        public void FailOnce(Value value) { if (++Attempts == 1) throw new RegisteredException(); }
    }
    private sealed class Hook(string name, List<string> calls, bool failAfter = false) : IStepHook
    {
        public async Task BeforeStepAsync(Step step) { await Task.Yield(); calls.Add(name + " before"); }
        public async Task AfterStepAsync(Step step)
        {
            await Task.Yield();
            calls.Add(name + " after " + step.Status);
            if (failAfter) throw new InvalidOperationException("after hook");
        }
    }
    private sealed class Capture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "same-id";
        public int ArgumentReads { get; private set; }
        private Value Argument { get { ArgumentReads++; return new Value(); } }

        public Then BuildWithArgument(string argumentSource)
        {
            var builder = argumentSource switch
            {
                "generated" => When(() => Keep(Argument)),
                "explicit" => When(() => Keep(Argument)).WithArgs(() => Argument),
                "fallback" => When(() => { }, callerFile: "no-catalog.cs", callerLine: 1).WithArgs(() => Argument),
                _ => throw new ArgumentOutOfRangeException(nameof(argumentSource))
            };
            return builder.Then(() => Complete());
        }
    }
}
