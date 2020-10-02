using BDTest.Interfaces.Internal;

namespace BDTest
{
    internal class BDTestServiceProvider
    {
        static BDTestServiceProvider()
        {
            var typeMatcher = new TypeMatcher();
            var scenarioRetryManager = new ScenarioRetryManager(typeMatcher);
            ScenarioExecutor = new ScenarioExecutor(scenarioRetryManager);
        }
        internal static IScenarioExecutor ScenarioExecutor { get; }
    }
}