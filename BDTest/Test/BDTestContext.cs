namespace BDTest.Test
{
    public class BDTestContext<TTestContext>
    {
        public BDTestBase TestBase { get; }
        public TTestContext TestContext { get; internal set; }

        internal BDTestContext(BDTestBase testBase, TTestContext testContext)
        {
            TestBase = testBase;
            TestContext = testContext;
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