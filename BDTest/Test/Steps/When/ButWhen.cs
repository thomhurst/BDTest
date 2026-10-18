namespace BDTest.Test.Steps.When;

public class ButWhen : When
{
    protected override StepType StepType { get; } = StepType.ButWhen;

    internal ButWhen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public new ButWhen WithArgs(params Func<object>[] arguments)
    {
        return WithArgs<ButWhen>(arguments);
    }

    public new ButWhen WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<ButWhen>(overridingStepText);
    }
}
