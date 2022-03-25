using BDTest.Test;

namespace BDTest.Interfaces.Internal;

internal interface IScenarioRetryManager
{
    Task CheckIfAlreadyExecuted(Scenario scenario);
}