using BDTest.Helpers;
using BDTest.Output;

namespace BDTest.Test.Steps;

public abstract class StepBuilder : BuildableTest
{
    internal readonly List<Step> ExistingSteps;
    protected abstract StepType StepType { get; }

    static StepBuilder()
    {
        Console.SetOut(TestOutputData.Instance);
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

        ExistingSteps = new List<Step> { new(runnable, stepType, Guid, ReportId, Run) };
            
        Run.Store.RegisterPending(this);
            
        StoryText = StoryTextHelper.GetStoryText(bdTestBase);
        ScenarioText = new ScenarioText(ScenarioTextHelper.GetScenarioText(TestMetadata, CallerMember));
        ParameterNames = TestMetadata.CopyParameters();
        CustomTestInformation = TestMetadata.CopyInformation();
        Tags = TestMetadata.CopyTags();
    }

    internal StepBuilder(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest)
    {
        Run = previousPartiallyBuiltTest.Run;
        Run.ThrowIfDisposed();
        TestOutputData.Run = Run;
        TestOutputData.FrameworkExecutionId = previousPartiallyBuiltTest.TestId;
        ScenarioTimeout = previousPartiallyBuiltTest.ScenarioTimeout;
        StoryText = previousPartiallyBuiltTest.StoryText;
        ScenarioText = previousPartiallyBuiltTest.ScenarioText;
        BdTestBase = previousPartiallyBuiltTest.BdTestBase;
        TestMetadata = previousPartiallyBuiltTest.TestMetadata;
        CallerMember = previousPartiallyBuiltTest.CallerMember;
        CallerFile = previousPartiallyBuiltTest.CallerFile;
        TestId = previousPartiallyBuiltTest.TestId;
        Guid = System.Guid.NewGuid().ToString();
        ReportId = previousPartiallyBuiltTest.ReportId;
        ParameterNames = previousPartiallyBuiltTest.ParameterNames;
        CustomTestInformation = previousPartiallyBuiltTest.CustomTestInformation;
        Tags = previousPartiallyBuiltTest.Tags;

        // An extension replaces its prefix in the not-run list, but sibling
        // extensions remain independent scenarios with their own step state.
        ExistingSteps = previousSteps.Select(step => new Step(step, Guid)).ToList();
        ExistingSteps.Add(new Step(runnable, StepType, Guid, ReportId, Run));

        if (previousPartiallyBuiltTest.Guid != null) Run.Store.NotRun.TryRemove(previousPartiallyBuiltTest.Guid, out _);
        Run.Store.RegisterPending(this);
        TestOutputData.TestId = Guid;
    }
}
