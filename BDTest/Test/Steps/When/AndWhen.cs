namespace BDTest.Test.Steps.When;

public sealed class AndWhen : WhenStepBuilder
{
    protected override StepType StepType { get; } = StepType.AndWhen;

    internal AndWhen(Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public AndWhen WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public AndWhen WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }
}
