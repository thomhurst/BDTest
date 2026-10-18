namespace BDTest.Test.Steps.Given;

public sealed class Given : GivenStepBuilder
{
    protected override StepType StepType { get; } = StepType.Given;

    internal Given(Runnable runnable, string? callerMember, string? callerFile, string testId, string reportId, BDTestBase bdTestBase) : base(runnable, callerMember, callerFile, testId, reportId, bdTestBase)
    {
    }

    /// <summary>Supplies lazy step-text arguments, evaluated once per executed attempt.</summary>
    public Given WithArgs(params Func<object?>[] arguments)
    {
        SetArguments(arguments);
        return this;
    }

    public Given WithStepText(Func<string?>? overridingStepText)
    {
        SetStepText(overridingStepText);
        return this;
    }
}
