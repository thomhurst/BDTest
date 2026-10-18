namespace BDTest.Test.Steps.Then;

public sealed class Then : ThenStepBuilder
{
    protected override StepType StepType { get; } = StepType.Then;

    internal Then(Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public Then WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public Then WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }

    public Then WithScenarioText(string scenarioText)
    {
        SetScenarioText(scenarioText);
        return this;
    }
}
