namespace BDTest.Test.Steps.Given;

public sealed class AndGiven : GivenStepBuilder
{
    protected override StepType StepType { get; } = StepType.AndGiven;

    internal AndGiven(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public AndGiven WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public AndGiven WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }
}
