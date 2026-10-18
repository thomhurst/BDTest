namespace BDTest.Test.Steps.Given;

public class ButGiven : AndGiven
{
    protected override StepType StepType { get; } = StepType.ButGiven;

    internal ButGiven(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public new ButGiven WithArgs(params Func<object?>[] arguments)
    {
        return WithArgs<ButGiven>(arguments);
    }

    public new ButGiven WithStepText(Func<string?>? overridingStepText)
    {
        return WithStepText<ButGiven>(overridingStepText);
    }
}
