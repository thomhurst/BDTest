using BDTest.Interfaces.Internal;

namespace BDTest
{
    internal class BDTestServiceProvider
    {
        internal static IScenarioExecutor ScenarioExecutor { get; } = new ScenarioExecutor();
    }
}