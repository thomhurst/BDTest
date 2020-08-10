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
        
        public string GetStoryText()
        {
            return TestBase.GetStoryText();
        }

        public string GetScenarioText()
        {
            return TestBase.GetScenarioText();
        }
    }
}