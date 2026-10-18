using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest.Test.Steps.Then;

public class Then : RunnableStepBuilder
{
    protected override StepType StepType { get; } = StepType.Then;

    internal Then(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public Then WithArgs(params Func<object?>[] arguments)
    {
        return WithArgs<Then>(arguments);
    }

    public Then WithStepText(Func<string?>? overridingStepText)
    {
        return WithStepText<Then>(overridingStepText);
    }

    [OverloadResolutionPriority(-1)]
    public AndThen And(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndThen And(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndThen And(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public ButThen But(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButThen But(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButThen But(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }
}
