using BDTest.Test;
using BDTest.Helpers;
using BDTest.Output;

namespace BDTest;

public abstract class StepBuilder : BuildableTest
{
    internal List<StepDefinition> ExistingSteps => Definition.Steps;
    protected abstract StepType StepType { get; }

    static StepBuilder()
    {
        Console.SetOut(TestOutputData.Instance.SynchronizedWriter);
    }

    protected void SetStepText(Func<string?>? overridingStepText)
    {
        ExistingSteps.Last().OverriddenStepText = overridingStepText;
    }

    protected void SetArguments(Func<object?>[] arguments)
    {
        if (arguments == null) throw new ArgumentNullException(nameof(arguments));
        if (arguments.Any(argument => argument == null)) throw new ArgumentException("Argument readers cannot be null.", nameof(arguments));
        ExistingSteps.Last().ExplicitArguments = (Func<object?>[])arguments.Clone();
    }

    // First Test Construction - Given or When
    internal StepBuilder(Runnable runnable, string? callerMember, string? callerFile, string testId, string reportId, StepType stepType, BDTestBase bdTestBase)
    {
        Run = bdTestBase.TestRun;
        Run.ThrowIfDisposed();
        TestOutputData.Run = Run;
        BdTestBase = bdTestBase;
        TestMetadata = bdTestBase.TestMetadata;
        CallerMember = callerMember;
        CallerFile = callerFile;
        TestId = testId;
        ReportId = reportId;

        Guid = System.Guid.NewGuid().ToString();
        TestOutputData.TestId = Guid;
        TestOutputData.FrameworkExecutionId = testId;

        ExistingSteps.Add(new StepDefinition(runnable, stepType));

        StoryText = StoryTextHelper.GetStoryText(bdTestBase);
        ScenarioText = new ScenarioText(ScenarioTextHelper.GetScenarioText(TestMetadata, CallerMember));
        ParameterNames = TestMetadata.CopyParameters();
        CustomTestInformation = TestMetadata.CopyInformation();
        Tags = TestMetadata.CopyTags();
        Run.Store.RegisterPending(Definition);
    }

    internal StepBuilder(Runnable runnable, BuildableTest previousPartiallyBuiltTest)
    {
        Definition = previousPartiallyBuiltTest.Definition.Snapshot();
        Run.ThrowIfDisposed();
        TestOutputData.Run = Run;
        TestOutputData.FrameworkExecutionId = TestId;
        Guid = System.Guid.NewGuid().ToString();
        ExistingSteps.Add(new StepDefinition(runnable, StepType));

        if (previousPartiallyBuiltTest.Guid != null) Run.Store.NotRun.TryRemove(previousPartiallyBuiltTest.Guid, out _);
        Run.Store.RegisterPending(Definition);
        TestOutputData.TestId = Guid;
    }
}
