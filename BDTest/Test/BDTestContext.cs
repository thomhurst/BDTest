using BDTest.Maps;
using BDTest.Output;

namespace BDTest.Test;

public class BDTestContext<TTestContext> : BDTestContext
{
    public TTestContext TestContext { get; internal set; }

    internal BDTestContext(BDTestBase testBase, TTestContext testContext, string bdTestExecutionId) : base(testBase, bdTestExecutionId)
    {
        TestContext = testContext;
    }
}

public class BDTestContext
{
    internal BDTestContext(BDTestBase testBase, string bdTestExecutionId)
    {
        TestBase = testBase;
        CurrentScenarioBDTestExecutionId = bdTestExecutionId;
    }

    public BDTestBase TestBase { get; }

    public string CurrentScenarioBDTestExecutionId { get; }

    private string _storyText;
    public string GetStoryText()
    {
        if (!string.IsNullOrEmpty(_storyText))
        {
            return _storyText;
        }
            
        return _storyText = TestBase.GetStoryText();
    }

    private string _scenarioText;
    public string GetScenarioText()
    {
        if (!string.IsNullOrEmpty(_scenarioText))
        {
            return _scenarioText;
        }

        return _scenarioText = TestBase.GetScenarioText() ?? CurrentScenario?.GetScenarioText();
    }

    /// <summary>The executing scenario, or null while an outline example is being constructed.</summary>
    public Scenario CurrentScenario
    {
        get
        {
            // No row exists yet; the framework mapping may still contain the previous example.
            if (TestOutputData.ConstructionOutput?.FrameworkExecutionId == CurrentScenarioBDTestExecutionId)
                return null;
            return TestHolder.ScenariosByTestFrameworkId.TryGetValue(CurrentScenarioBDTestExecutionId, out var scenario) ? scenario : null;
        }
    }

    public string CurrentTestRunnerId => TestHolder.CurrentReportId;
}
