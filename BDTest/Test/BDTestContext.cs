using System.Linq;
using BDTest.Maps;

namespace BDTest.Test
{
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
            BDTestExecutionId = bdTestExecutionId;
        }

        public BDTestBase TestBase { get; }

        public string BDTestExecutionId { get; }

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

            return _scenarioText = TestBase.GetScenarioText() ?? Scenario?.GetScenarioText();
        }

        public Scenario Scenario => TestHolder.Scenarios.Values.FirstOrDefault(scenario => scenario.FrameworkTestId == BDTestExecutionId);

        public string TestRunnerId => TestHolder.InstanceGuid;
    }
}