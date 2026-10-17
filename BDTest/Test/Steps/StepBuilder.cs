using BDTest.Extensions;
using BDTest.Helpers;
using BDTest.Maps;
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

    protected T WithStepText<T>(Func<string> overridingStepText) where T : StepBuilder
    {
        ExistingSteps.Last().OverriddenStepText = overridingStepText;
        return (T) this;
    }

    // First Test Construction - Given or When
    internal StepBuilder(Runnable runnable, string callerMember, string callerFile, string testId, string reportId, StepType stepType, BDTestBase bdTestBase)
    {
        BdTestBase = bdTestBase;
        CallerMember = callerMember;
        CallerFile = callerFile;
        TestId = testId;
        ReportId = reportId;
        
        Guid = System.Guid.NewGuid().ToString();
        TestOutputData.TestId = Guid;
        TestOutputData.FrameworkExecutionId = testId;
            
        ExistingSteps = new List<Step> { new(runnable, stepType, Guid, ReportId) };
            
        TestHolder.NotRun[Guid] = this;
            
        StoryText = StoryTextHelper.GetStoryText(bdTestBase);
        SetScenarioText();
        CustomTestInformation = TestInformationAttributeHelper.GetTestInformationAttributes(this.GetTestMethod());
    }

    internal StepBuilder(List<Step> previousSteps, Runnable runnable, BuildableTest previousPartiallyBuiltTest)
    {
        StoryText = previousPartiallyBuiltTest.StoryText;
        ScenarioText = previousPartiallyBuiltTest.ScenarioText;
        BdTestBase = previousPartiallyBuiltTest.BdTestBase;
        CallerMember = previousPartiallyBuiltTest.CallerMember;
        CallerFile = previousPartiallyBuiltTest.CallerFile;
        TestId = previousPartiallyBuiltTest.TestId;
        Guid = System.Guid.NewGuid().ToString();
        ReportId = previousPartiallyBuiltTest.ReportId;
        Parameters = previousPartiallyBuiltTest.Parameters;
        CustomTestInformation = previousPartiallyBuiltTest.CustomTestInformation;

        // An extension replaces its prefix in the not-run list, but sibling
        // extensions remain independent scenarios with their own step state.
        ExistingSteps = previousSteps.Select(step => new Step(step, Guid)).ToList();
        ExistingSteps.Add(new Step(runnable, StepType, Guid, ReportId));

        TestHolder.NotRun.TryRemove(previousPartiallyBuiltTest.Guid, out _);
        TestHolder.NotRun[Guid] = this;
        TestOutputData.TestId = Guid;
    }
        
    private void SetScenarioText()
    {
        var scenarioTextWithParameters = ScenarioTextHelper.GetScenarioTextWithParameters(this.GetTestMethod(), CallerMember);
        ScenarioText = scenarioTextWithParameters.ScenarioText;
        Parameters = scenarioTextWithParameters.Parameters;
    }
}