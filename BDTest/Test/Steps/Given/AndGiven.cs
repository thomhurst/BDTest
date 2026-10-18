using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest.Test.Steps.Given;

public class AndGiven : StepBuilder
{
    protected override StepType StepType { get; } = StepType.AndGiven;

    internal AndGiven(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public AndGiven WithArgs(params Func<object>[] arguments)
    {
        return WithArgs<AndGiven>(arguments);
    }

    public AndGiven WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<AndGiven>(overridingStepText);
    }

    [OverloadResolutionPriority(-1)]
    public AndGiven And(Action step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndGiven And(Func<Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndGiven And(Func<CancellationToken, Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And(Func<ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And(Func<CancellationToken, ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And<T>(Func<ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndGiven(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndGiven And<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndGiven(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public When.When When(Action step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When.When(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public When.When When(Func<Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When.When(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public When.When When(Func<CancellationToken, Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When.When(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When(Func<ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When.When(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When(Func<CancellationToken, ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When.When(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When<T>(Func<ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When.When(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public When.When When<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new When.When(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public ButGiven But(Action step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButGiven But(Func<Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButGiven But(Func<CancellationToken, Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But(Func<ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But(Func<CancellationToken, ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButGiven(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But<T>(Func<ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButGiven(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButGiven But<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButGiven(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }
}
