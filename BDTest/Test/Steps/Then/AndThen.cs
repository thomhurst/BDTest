namespace BDTest.Test.Steps.Then;

public class AndThen : Then
{
    protected override StepType StepType { get; } = StepType.AndThen;

    // Actions
    internal AndThen(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(previousSteps, runnable, previousPartiallyBuiltTest)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public new AndThen WithArgs(params Func<object>[] arguments)
    {
        return WithArgs<AndThen>(arguments);
    }

    public new AndThen WithStepText(Func<string> overridingStepText)
    {
        return WithStepText<AndThen>(overridingStepText);
    }
}
