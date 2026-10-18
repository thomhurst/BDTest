using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest.Test.Steps.When;

public class When : StepBuilder
{
    protected override StepType StepType { get; } = StepType.When;

    internal When(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    internal When(Runnable runnable, string callerMember, string callerFile, string testId, string reportId, BDTestBase bdTestBase) : base(runnable, callerMember, callerFile, testId, reportId, StepType.When, bdTestBase)
    {
        // Used for skipping a 'Given' step
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public When WithArgs(params Func<object>[] arguments)
    {
        return WithArgs<When>(arguments);
    }

    public When WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<When>(overridingStepText);
    }

    [OverloadResolutionPriority(-1)]
    public Then.Then Then(Action step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Then.Then(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public Then.Then Then(Func<Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Then.Then(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public Then.Then Then(Func<CancellationToken, Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Then.Then(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public Then.Then Then(Func<ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Then.Then(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public Then.Then Then(Func<CancellationToken, ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Then.Then(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public Then.Then Then<T>(Func<ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Then.Then(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public Then.Then Then<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new Then.Then(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public AndWhen And(Action step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndWhen And(Func<Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public AndWhen And(Func<CancellationToken, Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndWhen And(Func<ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndWhen And(Func<CancellationToken, ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndWhen And<T>(Func<ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndWhen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public AndWhen And<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new AndWhen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(-1)]
    public ButWhen But(Action step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButWhen But(Func<Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    public ButWhen But(Func<CancellationToken, Task> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButWhen But(Func<ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButWhen But(Func<CancellationToken, ValueTask> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButWhen(ExistingSteps, new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButWhen But<T>(Func<ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButWhen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    public ButWhen But<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string argumentExpression = null)
    {
        return new ButWhen(ExistingSteps, Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }
}
