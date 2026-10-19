using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest;

/// <summary>A runnable chain of generic steps without Given/When/Then ordering.</summary>
public sealed class GenericStep : RunnableStepBuilder
{
    protected override StepType StepType => StepType.Step;

    internal GenericStep(Runnable runnable, string? callerMember, string? callerFile, string testId, string reportId, BDTestBase bdTestBase)
        : base(runnable, callerMember, callerFile, testId, reportId, StepType.Step, bdTestBase) { }

    internal GenericStep(Runnable runnable, BuildableTest previous) : base(runnable, previous) { }

    public GenericStep WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public GenericStep WithStepText(Func<string?>? text)
    {
        SetStepText(text);
        return this;
    }

    public GenericStep WithScenarioText(string text)
    {
        SetScenarioText(text);
        return this;
    }

    [OverloadResolutionPriority(-1)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026", Justification = "Caller information and delegate overloads follow the existing step continuations.")]
    public GenericStep Step(Action step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new GenericStep(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026", Justification = "Caller information and delegate overloads follow the existing step continuations.")]
    public GenericStep Step(Func<Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new GenericStep(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(1)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026", Justification = "Caller information and delegate overloads follow the existing step continuations.")]
    public GenericStep Step(Func<CancellationToken, Task> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new GenericStep(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026", Justification = "Caller information and delegate overloads follow the existing step continuations.")]
    public GenericStep Step(Func<ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new GenericStep(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026", Justification = "Caller information and delegate overloads follow the existing step continuations.")]
    public GenericStep Step(Func<CancellationToken, ValueTask> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new GenericStep(new Runnable(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026", Justification = "Caller information and delegate overloads follow the existing step continuations.")]
    public GenericStep Step<T>(Func<ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new GenericStep(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

    [OverloadResolutionPriority(0)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026", Justification = "Caller information and delegate overloads follow the existing step continuations.")]
    public GenericStep Step<T>(Func<CancellationToken, ValueTask<T>> step, [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0, [CallerArgumentExpression(nameof(step))] string? argumentExpression = null)
    {
        return new GenericStep(Runnable.FromResult(step, argumentExpression, callerFile, callerLine), this);
    }

}
