using BDTest.Interfaces;
using Microsoft.Extensions.Time.Testing;
using BDTest.Settings;
using BDTest.Test;
using Step = BDTest.Contracts.StepResult;
using Scenario = BDTest.Contracts.ScenarioResult;

namespace BDTest.Tests.Fixtures;

public class ExecutionCancellationTests
{
    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Blocking_scenario_token_callback_cannot_prevent_step_cancellation(bool framework)
    {
        using var source = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, 2);
        using var run = BDTestRun.Start(options);
        var scenarioToken = CancellationToken.None;
        var test = new Host(run, framework ? source.Token : default)
        {
            BeforeScenario = token => { scenarioToken = token; return Task.CompletedTask; }
        };
        var entered = Signal();
        var callbackEntered = Signal();
        var stepFinished = Signal();
        var later = false;
        CancellationTokenRegistration registration = default;
        try
        {
            var execution = test.When(async token =>
            {
                // This newer registration runs before the step's parent-token registration.
                registration = scenarioToken.Register(() =>
                {
                    callbackEntered.TrySetResult(true);
                    release.Wait();
                });
                entered.TrySetResult(true);
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { stepFinished.TrySetResult(true); }
            }).Then(() => later = true).BDTestAsync(framework ? default : source.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            source.Cancel();
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
            await stepFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(later).IsFalse();
            await Assert.That(test.ContextCreations).IsEqualTo(1);
            await Assert.That(run.Scenarios.Single().Status).IsEqualTo(Status.Failed);
            await Assert.That(run.Scenarios.Single().RetryCount).IsZero();
        }
        finally { release.Set(); registration.Dispose(); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Cancellation_after_a_successful_outline_row_cannot_return_a_passing_partial_outline(bool framework)
    {
        using var source = new CancellationTokenSource();
        var clock = new CallbackClock();
        var options = new BDTestOptions { TimeProvider = clock };
        options.SuccessExceptionTypes.Add(typeof(OperationCanceledException));
        using var run = BDTestRun.Start(options);
        var test = new Host(run, framework ? source.Token : default)
        {
            // The next wall-clock read finalizes the row after all its callbacks succeeded.
            AfterScenario = _ => { clock.OnNextUtcRead = source.Cancel; return Task.CompletedTask; }
        };
        var factories = 0;
        var execution = test.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, _ =>
        {
            factories++;
            return test.When(() => { }).Then(() => { });
        }).BDTestAsync(framework ? default : source.Token);
        await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
        await Assert.That(factories).IsEqualTo(1);
        await Assert.That(test.ContextCreations).IsEqualTo(1);
        var outline = run.Scenarios.Single();
        await Assert.That(outline.Status).IsEqualTo(Status.Failed);
        await Assert.That(outline.TimedOut).IsFalse();
        await Assert.That(Required(outline.Exception).TypeName).IsEqualTo(typeof(OperationCanceledException).FullName);
        await Assert.That(outline.Examples.Single().Status).IsEqualTo(Status.Passed);
    }

    [Test]
    public async Task Late_argument_reader_cannot_publish_arguments_or_start_the_step_body()
    {
        var clock = new FakeTimeProvider();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        using var release = new ManualResetEventSlim();
        var completed = Signal();
        var body = Signal();
        var builder = new Host(run).When(() => { body.TrySetResult(true); }).WithDocString(() =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            release.Wait();
            completed.TrySetResult(true);
            return new StepDocString("late argument");
        }).WithStepTimeout(TimeSpan.FromMilliseconds(100)).Then(() => { });
        try
        {
            await Assert.That(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).Throws<TimeoutException>();
            var step = Required(run.Scenarios.Single().Steps)[0];
            release.Set();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(await Task.WhenAny(body.Task, Task.Delay(250))).IsNotSameReferenceAs(body.Task);
            await Assert.That(step.Arguments).IsEmpty();
            await Assert.That(step.Status).IsEqualTo(Status.Failed);
            await Assert.That(body.Task.IsCompleted).IsFalse();
        }
        finally { release.Set(); }
    }

    [Test]
    public async Task Cancellation_during_retry_hook_prevents_context_replacement()
    {
        using var source = new CancellationTokenSource();
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, 2);
        using var run = BDTestRun.Start(options);
        var entered = Signal();
        var test = new Host(run)
        {
            BeforeRetry = async token => { entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, token); }
        };
        var execution = test.When(() => throw new InvalidOperationException("retry"))
            .Then(() => { }).BDTestAsync(source.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        source.Cancel();
        await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
        await Assert.That(test.ContextCreations).IsEqualTo(1);
        await Assert.That(test.RetrySetupTokens).IsEmpty();
        await Assert.That(run.Scenarios.Single().Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(run.Scenarios.Single().Exception).Message).Contains("cancelled");
    }

    [Test]
    public async Task Attributed_retry_hooks_receive_the_effective_token()
    {
        using var source = new CancellationTokenSource();
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, 1);
        using var run = BDTestRun.Start(options);
        var test = new Host(run);
        var calls = 0;
        var scenario = await test.When(() => { if (calls++ == 0) throw new InvalidOperationException("retry"); })
            .Then(() => { }).BDTestAsync(source.Token);
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(test.RetrySetupTokens.Single().CanBeCanceled).IsTrue();
        await Assert.That(test.RetrySetupTokens.Single().IsCancellationRequested).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Explicit_and_framework_tokens_cancel_running_steps_without_a_timeout(bool framework)
    {
        using var source = new CancellationTokenSource();
        var options = new BDTestOptions();
        options.RetryTestRules.Add(_ => true, 3);
        options.SuccessExceptionTypes.Add(typeof(OperationCanceledException));
        using var run = BDTestRun.Start(options);
        var test = new Host(run, framework ? source.Token : default);
        var entered = Signal();
        var finished = false;
        var later = false;
        var execution = test.When(async token =>
        {
            entered.SetResult(true);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { finished = true; }
        }).Then(() => later = true).BDTestAsync(framework ? default : source.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        source.Cancel();
        var failure = await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
        var report = run.Scenarios.Single();
        await Assert.That(Required(failure).Message).Contains("caller or test framework");
        await Assert.That(finished).IsTrue();
        await Assert.That(later).IsFalse();
        await Assert.That(report.Status).IsEqualTo(Status.Failed);
        await Assert.That(report.TimedOut).IsFalse();
        await Assert.That(report.RetryCount).IsZero();
        await Assert.That(Required(report.Steps)[1].Status).IsEqualTo(Status.NotRun);
    }

    [Test]
    public async Task Precancelled_scenario_never_initializes_context_or_starts_hooks_or_steps()
    {
        using var run = BDTestRun.Start();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var test = new Host(run) { BeforeScenario = _ => throw new AssertionException("before hook ran") };
        await Assert.That(async () => await test.When(() => throw new AssertionException("body ran"))
            .Then(() => { }).BDTestAsync(source.Token)).Throws<OperationCanceledException>();
        await Assert.That(test.ContextCreations).IsZero();
        await Assert.That(Required(run.Scenarios.Single().Steps).All(step => step.Status == Status.NotRun)).IsTrue();
    }

    [Test]
    [Arguments("base step")]
    [Arguments("registered step")]
    [Arguments("base scenario")]
    [Arguments("registered scenario")]
    public async Task Cancellation_reaches_existing_hook_contracts_and_cleanup(string where)
    {
        using var source = new CancellationTokenSource();
        var entered = Signal();
        var cleanup = new List<bool>();
        async Task Before(CancellationToken token)
        {
            entered.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
        }
        Task After(CancellationToken token) { cleanup.Add(token.IsCancellationRequested); return Task.CompletedTask; }
        var options = new BDTestOptions();
        if (where == "registered step") options.StepHooks.Add(new StepHook(Before, After));
        if (where == "registered scenario") options.ScenarioHooks.Add(new ScenarioHook(Before, After));
        using var run = BDTestRun.Start(options);
        var test = new Host(run);
        if (where == "base step") { test.BeforeStep = Before; test.AfterStep = After; }
        if (where == "base scenario") { test.BeforeScenario = Before; test.AfterScenario = After; }
        var execution = test.When(() => throw new AssertionException("body ran")).Then(() => { }).BDTestAsync(source.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        source.Cancel();
        await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
        await Assert.That(cleanup).IsEquivalentTo(new[] { true });
        await Assert.That(run.Scenarios.Single().TimedOut).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Cancellation_stops_outline_rows_before_context_replacement(bool framework)
    {
        using var source = new CancellationTokenSource();
        using var run = BDTestRun.Start();
        var test = new Host(run, framework ? source.Token : default);
        var entered = Signal();
        var factories = 0;
        var execution = test.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
        {
            factories++;
            return test.When(async token => { entered.SetResult(true); await Task.Delay(Timeout.Infinite, token); }).Then(() => { });
        }).BDTestAsync(framework ? default : source.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        source.Cancel();
        await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
        await Assert.That(factories).IsEqualTo(1);
        await Assert.That(test.ContextCreations).IsEqualTo(1);
        var outline = run.Scenarios.Single(s => s.IsOutline);
        await Assert.That(outline.Status).IsEqualTo(Status.Failed);
        await Assert.That(outline.TimedOut).IsFalse();
        await Assert.That(outline.Examples).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Abandoned_work_cannot_retry_replace_context_or_append_late_output(bool outline)
    {
        var clock = new FakeTimeProvider();
        var options = new BDTestOptions { TimeProvider = clock };
        options.RetryTestRules.Add(_ => true, 2);
        using var run = BDTestRun.Start(options);
        var test = new Host(run);
        var finish = Signal();
        var completed = Signal();
        object? originalContext = null;
        object? lateContext = null;
        async Task Work()
        {
            originalContext = test.Context;
            Console.WriteLine("early output");
            clock.Advance(TimeSpan.FromMilliseconds(50));
            try
            {
                await finish.Task;
                lateContext = test.Context;
                Console.WriteLine("late abandoned output");
                test.WriteLateOutput();
            }
            finally { completed.TrySetResult(true); }
        }
        RunnableStepBuilder Build() => test.When(Work).WithStepTimeout(TimeSpan.FromMilliseconds(50)).Then(() => { });
        try
        {
            var execution = outline
                ? test.ScenarioOutline("row <Value>", new[] { new { Value = 1 }, new { Value = 2 } }, _ => Build()).BDTestAsync()
                : Build().BDTestAsync();
            var failure = await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(10))).Throws<TimeoutException>();
            await Assert.That(Required(failure).Message).Contains("Cancellation was ignored").And.Contains("retries disabled");
            await Assert.That(test.ContextCreations).IsEqualTo(1);
            var root = run.Scenarios.Single();
            var report = outline ? Required(root.Examples).Single() : root;
            await Assert.That(report.RetryCount).IsZero();
            await Assert.That(report.TimedOut).IsTrue();
            await Assert.That(report.Output).Contains("early output");
            finish.SetResult(true);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            root = run.Scenarios.Single();
            report = outline ? root.Examples.Single() : root;
            await Assert.That(lateContext).IsSameReferenceAs(originalContext);
            await Assert.That(report.Output).DoesNotContain("late abandoned");
            await Assert.That(report.TearDownOutput ?? "").DoesNotContain("late abandoned");
            await Assert.That(report.CustomHtmlOutputForReport ?? "").DoesNotContain("late abandoned");
            var next = await new Host(run).When(() => Console.WriteLine("next scenario")).Then(() => { }).BDTestAsync();
            await Assert.That(next.Output).Contains("next scenario").And.DoesNotContain("late abandoned");
        }
        finally { finish.TrySetResult(true); await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Test]
    [Arguments("scenario before")]
    [Arguments("scenario after")]
    [Arguments("step before")]
    [Arguments("step after")]
    public async Task Timeout_covers_hooks_and_preserves_flags(string where)
    {
        var clock = new FakeTimeProvider();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var test = new Host(run);
        var cancelled = false;
        async Task Hook(CancellationToken token)
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancelled = true; }
        }
        if (where == "scenario before") test.BeforeScenario = Hook;
        if (where == "scenario after") test.AfterScenario = Hook;
        if (where == "step before") test.BeforeStep = Hook;
        if (where == "step after") test.AfterStep = Hook;
        var builder = test.When(() => { }).WithStepTimeout(TimeSpan.FromMilliseconds(100)).Then(() => { });
        if (where.StartsWith("scenario")) _ = builder.WithScenarioTimeout(TimeSpan.FromMilliseconds(100));
        await Assert.That(async () => await builder.BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).Throws<TimeoutException>();
        await Assert.That(cancelled).IsTrue();
        var report = run.Scenarios.Single();
        await Assert.That(report.TimedOut).IsTrue();
        await Assert.That(report.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(report.Steps)[0].TimedOut).IsEqualTo(where.StartsWith("step"));
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(report));
        await Assert.That(restored.TimedOut).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Completed_last_hook_cannot_hide_an_expired_budget(bool scenarioHook)
    {
        var clock = new FakeTimeProvider();
        using var run = BDTestRun.Start(new BDTestOptions { TimeProvider = clock });
        var test = new Host(run);
        var hookCalls = 0;
        Task Hook(CancellationToken token)
        {
            if (scenarioHook || ++hookCalls == 2) clock.Advance(TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        }
        if (scenarioHook) test.AfterScenario = Hook;
        else test.AfterStep = Hook;
        await Assert.That(async () => await test.When(() => { }).Then(() => { })
            .WithScenarioTimeout(TimeSpan.FromSeconds(1)).BDTestAsync()).Throws<TimeoutException>();
        await Assert.That(run.Scenarios.Single().TimedOut).IsTrue();
    }

    private sealed class Host(BDTestRun run, CancellationToken framework = default) : AbstractContextBDTestBase<State>
    {
        private readonly string _id = Guid.NewGuid().ToString("N");
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => _id;
        protected override CancellationToken FrameworkCancellationToken => framework;
        internal int ContextCreations;
        internal Func<CancellationToken, Task>? BeforeStep, AfterStep, BeforeScenario, AfterScenario, BeforeRetry;
        internal readonly List<CancellationToken> RetrySetupTokens = new();
        [BDTest.BDTestRetrySetUp]
        public void RetrySetup(CancellationToken cancellationToken) => RetrySetupTokens.Add(cancellationToken);
        protected override ValueTask<State> CreateContextAsync(ContextCreation creation)
        { ContextCreations++; return new ValueTask<State>(new State()); }
        public override Task OnBeforeStep(Step step, CancellationToken cancellationToken) => BeforeStep?.Invoke(cancellationToken) ?? Task.CompletedTask;
        public override Task OnAfterStep(Step step, CancellationToken cancellationToken) => AfterStep?.Invoke(cancellationToken) ?? Task.CompletedTask;
        public override Task OnBeforeScenario(ScenarioHookContext<State> scenario) => BeforeScenario?.Invoke(scenario.CancellationToken) ?? Task.CompletedTask;
        public override Task OnAfterScenario(ScenarioHookContext<State> scenario) => AfterScenario?.Invoke(scenario.CancellationToken) ?? Task.CompletedTask;
        public override Task OnBeforeRetry(CancellationToken cancellationToken) => BeforeRetry?.Invoke(cancellationToken) ?? Task.CompletedTask;
        internal void WriteLateOutput()
        {
            BDTest.Output.TestOutputData.CollectStartupOutput(_id, "late abandoned startup", false, run: run);
            BDTest.Output.TestOutputData.CollectTearDownOutput(_id, "late abandoned teardown", false, run);
            BDTest.Output.TestOutputData.WriteCustomHtmlForReport(_id, "late abandoned html", run);
        }
    }

    private sealed class CallbackClock : TimeProvider
    {
        internal Action? OnNextUtcRead;
        public override DateTimeOffset GetUtcNow()
        {
            Interlocked.Exchange(ref OnNextUtcRead, null)?.Invoke();
            return base.GetUtcNow();
        }
    }

    private sealed class State;
    private sealed class StepHook(Func<CancellationToken, Task> before, Func<CancellationToken, Task> after) : IStepHook
    {
        public Task BeforeStepAsync(Step step, CancellationToken cancellationToken) => before(cancellationToken);
        public Task AfterStepAsync(Step step, CancellationToken cancellationToken) => after(cancellationToken);
    }
    private sealed class ScenarioHook(Func<CancellationToken, Task> before, Func<CancellationToken, Task> after) : IScenarioHook
    {
        public Task BeforeScenarioAsync(ScenarioHookContext scenario) => before(scenario.CancellationToken);
        public Task AfterScenarioAsync(ScenarioHookContext scenario) => after(scenario.CancellationToken);
    }
}
