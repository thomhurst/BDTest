namespace BDTest;

public sealed class When : WhenStepBuilder
{
    protected override StepType StepType { get; } = StepType.When;

    internal When(Runnable runnable, BuildableTest previousPartiallyBuiltTest) : base(runnable, previousPartiallyBuiltTest)
    {
    }

    internal When(Runnable runnable, string? callerMember, string? callerFile, string testId, string reportId, BDTestBase bdTestBase) : base(runnable, callerMember, callerFile, testId, reportId, bdTestBase)
    {
        // Used for skipping a 'Given' step
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public When WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public When WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }
}
