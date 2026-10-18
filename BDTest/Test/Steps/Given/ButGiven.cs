namespace BDTest.Test.Steps.Given;

public class ButGiven : AndGiven
{
    protected override StepType StepType { get; } = StepType.ButGiven;

    internal ButGiven(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    public new ButGiven WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<ButGiven>(overridingStepText);
    }
}
