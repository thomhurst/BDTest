namespace BDTest.Test.Steps.Then;

public sealed class AndThen : ThenStepBuilder
{
    protected override StepType StepType { get; } = StepType.AndThen;

    // Actions
    internal AndThen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public AndThen WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public AndThen WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }

    public AndThen WithScenarioText(string scenarioText)
    {
        SetScenarioText(scenarioText);
        return this;
    }
}
