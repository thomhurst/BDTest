namespace BDTest.Test
{
    public class BDTestContext<TTestContext>
    {
        public BDTestBase TestBase { get; }
        public TTestContext TestContext { get; internal set; }

        public string BDTestExecutionId { get; }
        
        internal BDTestContext(BDTestBase testBase, TTestContext testContext, string bdTestExecutionId)
        {
            TestBase = testBase;
            TestContext = testContext;
            BDTestExecutionId = bdTestExecutionId;
        }

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
            
            return _scenarioText = TestBase.GetScenarioText();
        }
    }
}