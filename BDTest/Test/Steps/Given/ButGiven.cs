namespace BDTest;

public sealed class ButGiven : GivenStepBuilder
{
    protected override StepType StepType { get; } = StepType.ButGiven;

    internal ButGiven(Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public ButGiven WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public ButGiven WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }
}
