using BDTest.Test;

namespace BDTest.Interfaces.Internal;

internal interface IScenarioExecutor
{
    Task ExecuteAsync(Scenario scenario);
}