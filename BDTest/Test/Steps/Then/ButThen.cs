namespace BDTest.Test.Steps.Then;

public class ButThen : Then
{
    protected override StepType StepType { get; } = StepType.ButThen;

    internal ButThen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public new ButThen WithArgs(params Func<object>[] arguments)
    {
        return WithArgs<ButThen>(arguments);
    }

    public new ButThen WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<ButThen>(overridingStepText);
    }
}
