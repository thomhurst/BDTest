namespace BDTest.Test.Steps.When;

public class ButWhen : When
{
    protected override StepType StepType { get; } = StepType.ButWhen;

    internal ButWhen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    public new ButWhen WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<ButWhen>(overridingStepText);
    }
}
