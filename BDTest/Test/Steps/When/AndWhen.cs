namespace BDTest.Test.Steps.When;

public class AndWhen : When
{
    protected override StepType StepType { get; } = StepType.AndWhen;

    internal AndWhen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public new AndWhen WithArgs(params Func<object>[] arguments)
    {
        return WithArgs<AndWhen>(arguments);
    }

    public new AndWhen WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<AndWhen>(overridingStepText);
    }
}
