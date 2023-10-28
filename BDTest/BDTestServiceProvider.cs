using System.Runtime.CompilerServices;
using BDTest.Engine;
using BDTest.Interfaces.Internal;
using BDTest.Output;

namespace BDTest;

internal static class BDTestServiceProvider
{
    private static bool _alreadyRun;
    internal static IScenarioExecutor ScenarioExecutor { private set; get; }

    // ReSharper disable once UnusedMember.Global
    [ModuleInitializer]
    internal static void InitialiseBDTest()
    {
        if (_alreadyRun)
        {
            return;
        }

        _alreadyRun = true;

        InternalTestTimeData.TestsStartedAt = DateTime.Now;

        var scenarioRetryManager = new ScenarioRetryManager();
        ScenarioExecutor = new ScenarioExecutor(scenarioRetryManager);
    }
}