using System;
using BDTest.Engine;
using BDTest.Interfaces.Internal;
using BDTest.Output;
using BDTest.Settings;

namespace BDTest
{
    internal static class BDTestServiceProvider
    {
        static BDTestServiceProvider()
        {
            InitialiseBDTest();
            
            var scenarioRetryManager = new ScenarioRetryManager();
            ScenarioExecutor = new ScenarioExecutor(scenarioRetryManager);
        }

        private static void InitialiseBDTest()
        {
            if (BDTestSettings.InterceptConsoleOutput)
            {
                Console.SetOut(TestOutputData.Instance);
            }
        }

        internal static IScenarioExecutor ScenarioExecutor { get; }
    }
}