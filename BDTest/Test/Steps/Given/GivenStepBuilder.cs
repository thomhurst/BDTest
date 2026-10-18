using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest.Test.Steps.Given;

/// <summary>Shared continuations for given steps.</summary>
public abstract class GivenStepBuilder : StepBuilder
{
    internal GivenStepBuilder(Runnable runnable, BuildableTest previousPartiallyBuiltTest)
        : base(runnable, previousPartiallyBuiltTest)
    {
    }

    internal GivenStepBuilder(Runnable runnable, string? callerMember, string? callerFile, string testId, string reportId, BDTestBase bdTestBase)
        : base(runnable, callerMember, callerFile, testId, reportId, StepType.Given, bdTestBase)
    {
    }

    [OverloadResolutionPriority(-1)]
    public AndGiven And(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndGiven And(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndGiven And(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndGiven(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new AndGiven(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public When.When When(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When.When(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public When.When When(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When.When(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public When.When When(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When.When(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When.When(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When.When(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When.When(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new When.When(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public ButGiven But(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButGiven But(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButGiven But(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButGiven(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButGiven(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new ButGiven(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }
}
