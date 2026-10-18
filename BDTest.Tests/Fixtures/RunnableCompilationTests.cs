using System.Collections.Concurrent;
using System.Linq.Expressions;
using BDTest.Attributes;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Test.Steps;
using BDTest.Test.Steps.When;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class RunnableCompilationTests
{
    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task Repeated_execution_compiles_once_and_reads_current_state(string kind)
    {
        using var probe = new Probe();
        var runnable = probe.CreateRunnable(kind);
        Assert.That(probe.Compilation.Reductions, Is.Zero);
        Assert.That(probe.Values, Is.Empty);

        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        probe.Value = 10;
        await runnable.Run(firstCancellation.Token);
        probe.Value = 20;
        await runnable.Run(secondCancellation.Token);

        Assert.That(probe.Compilation.Reductions, Is.EqualTo(1));
        Assert.That(probe.Values, Is.EqualTo(new[] { 10, 20 }));
        if (kind == "CancellableTask")
            Assert.That(probe.Tokens, Is.EqualTo(new[] { firstCancellation.Token, secondCancellation.Token }));
    }

    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task A_step_failure_does_not_force_recompilation(string kind)
    {
        using var probe = new Probe { FailuresRemaining = 1 };
        var runnable = probe.CreateRunnable(kind);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runnable.Run());
        Assert.That(error.Message, Is.EqualTo("step failure"));

        await runnable.Run();

        Assert.That(probe.Values, Has.Count.EqualTo(2));
        Assert.That(probe.Compilation.Reductions, Is.EqualTo(1));
    }

    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task A_scenario_retry_reuses_compilation_and_reads_recreated_context(string kind)
    {
        using var probe = new Probe { FailuresRemaining = 1 };
        var scenario = await probe.CreateScenario(kind).Then(() => Console.WriteLine("done")).BDTestAsync();

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(probe.Compilation.Reductions, Is.EqualTo(1));
        Assert.That(probe.Contexts, Has.Count.EqualTo(2));
        Assert.That(probe.Contexts[0], Is.Not.SameAs(probe.Contexts[1]));
        Assert.That(probe.Contexts.Select(context => context.Calls), Is.EqualTo(new[] { 1, 1 }));
    }

    [TestCase("Action")]
    [TestCase("Task")]
    [TestCase("CancellableTask")]
    public async Task Skipped_steps_never_compile_or_invoke_the_expression(string kind)
    {
        using var probe = new Probe();
        var step = new Step(probe.CreateRunnable(kind), StepType.When, "scenario", "report");
        var previous = BDTestSettings.Debug.ShouldSkipWhenStep;
        try
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = true;
            await step.Execute(null);

            Assert.That(step.Status, Is.EqualTo(Status.Skipped));
            Assert.That(probe.Compilation.Reductions, Is.Zero);
            Assert.That(probe.Values, Is.Empty);
        }
        finally
        {
            BDTestSettings.Debug.ShouldSkipWhenStep = previous;
        }
    }

    [Test]
    public async Task Concurrent_execution_compiles_a_shared_definition_once()
    {
        var calls = new ConcurrentBag<int>();
        Expression<Action> source = () => calls.Add(1);
        var compilation = new CountingExpression(source.Body);
        var runnable = new Runnable(Expression.Lambda<Action>(compilation));

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => runnable.Run())));

        Assert.That(calls, Has.Count.EqualTo(16));
        Assert.That(compilation.Reductions, Is.EqualTo(1));
    }

    [Test]
    public async Task Compilation_errors_are_lazy_and_cached()
    {
        var compilation = new CountingExpression(Expression.Empty()) { Fail = true };
        var runnable = new Runnable(Expression.Lambda<Action>(compilation));
        Assert.That(compilation.Reductions, Is.Zero);

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => runnable.Run());
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => runnable.Run());

        Assert.That(first.Message, Is.EqualTo("compilation failure"));
        Assert.That(second.Message, Is.EqualTo(first.Message));
        Assert.That(compilation.Reductions, Is.EqualTo(1));
    }

    // Expression compilation reduces extension nodes. Count that operation directly,
    // without adding a compilation counter or test hook to production code.
    private sealed class CountingExpression(Expression body) : Expression
    {
        private int _reductions;
        public int Reductions => _reductions;
        public bool Fail { get; init; }
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => body.Type;
        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            Interlocked.Increment(ref _reductions);
            if (Fail)
                throw new InvalidOperationException("compilation failure");
            return body;
        }
    }

    public sealed class ProbeContext
    {
        public int Calls { get; set; }
    }

    [BDTestRetry(1)]
    private sealed class Probe : AbstractContextBDTestBase<ProbeContext>, IDisposable
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;
        public int Value { get; set; }
        public int FailuresRemaining { get; set; }
        public List<int> Values { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();
        public List<ProbeContext> Contexts { get; } = new();
        public CountingExpression Compilation { get; private set; }

        private LambdaExpression CreateExpression(string kind)
        {
            LambdaExpression source = kind switch
            {
                "Action" => (Expression<Action>)(() => Record()),
                "Task" => (Expression<Func<Task>>)(() => RecordAsync()),
                "CancellableTask" => (Expression<Func<CancellationToken, Task>>)(token => RecordAsync(token)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            Compilation = new CountingExpression(source.Body);
            return Expression.Lambda(source.Type, Compilation, source.Parameters);
        }

        public Runnable CreateRunnable(string kind) => CreateExpression(kind) switch
        {
            Expression<Action> step => new Runnable(step),
            Expression<Func<Task>> step => new Runnable(step),
            Expression<Func<CancellationToken, Task>> step => new Runnable(step),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public When CreateScenario(string kind) => CreateExpression(kind) switch
        {
            Expression<Action> step => When(step),
            Expression<Func<Task>> step => When(step),
            Expression<Func<CancellationToken, Task>> step => When(step),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private void Record()
        {
            Values.Add(Value);
            Contexts.Add(Context);
            Context.Calls++;
            if (FailuresRemaining-- > 0)
                throw new InvalidOperationException("step failure");
        }

        private async Task RecordAsync()
        {
            await Task.Yield();
            Record();
        }

        private async Task RecordAsync(CancellationToken token)
        {
            await Task.Yield();
            Tokens.Add(token);
            Record();
        }

        public void Dispose()
        {
            RemoveContext();
            MarkTestAsComplete();
        }
    }
}
