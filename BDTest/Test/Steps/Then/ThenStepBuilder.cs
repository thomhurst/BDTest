using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest;

/// <summary>Shared continuations for then steps.</summary>
public abstract class ThenStepBuilder : RunnableStepBuilder
{
    internal ThenStepBuilder(Runnable runnable, BuildableTest previousPartiallyBuiltTest)
        : base(runnable, previousPartiallyBuiltTest)
    {
    }

    [OverloadResolutionPriority(-1)]
    public AndThen And(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndThen And(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndThen And(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndThen And<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndThen(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public ButThen But(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButThen But(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButThen But(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButThen But<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButThen(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }
}
