namespace BDTest.Test.Steps.Then;

public class ButThen : Then
{
    protected override StepType StepType { get; } = StepType.ButThen;

    internal ButThen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    public new ButThen WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<ButThen>(overridingStepText);
    }
}
