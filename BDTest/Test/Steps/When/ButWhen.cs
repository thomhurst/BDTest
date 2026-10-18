namespace BDTest.Test.Steps.When;

public sealed class ButWhen : WhenStepBuilder
{
    protected override StepType StepType { get; } = StepType.ButWhen;

    internal ButWhen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public ButWhen WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public ButWhen WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }
}
