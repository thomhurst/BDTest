using System.Threading.Tasks;
using BDTest.Test;

namespace BDTest.Interfaces.Internal
{
    public interface IScenarioRetryManager
    {
        Task CheckIfAlreadyExecuted(Scenario scenario);
    }
}