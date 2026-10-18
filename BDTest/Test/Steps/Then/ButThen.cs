namespace BDTest.Test.Steps.Then;

public sealed class ButThen : ThenStepBuilder
{
    protected override StepType StepType { get; } = StepType.ButThen;

    internal ButThen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public ButThen WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public ButThen WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }

    public ButThen WithScenarioText(string scenarioText)
    {
        SetScenarioText(scenarioText);
        return this;
    }
}
