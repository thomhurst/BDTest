using System;
using BDTest.Engine;
using BDTest.Interfaces.Internal;
using BDTest.Output;
using BDTest.Settings;

namespace BDTest
{
    internal class BDTestServiceProvider
    {
        static BDTestServiceProvider()
        {
            InitialiseBDTest();
            
            var typeMatcher = new TypeMatcher();
            var scenarioRetryManager = new ScenarioRetryManager(typeMatcher);
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