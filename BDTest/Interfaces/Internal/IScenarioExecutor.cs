using System.Threading.Tasks;
using BDTest.Test;

namespace BDTest.Interfaces.Internal
{
    public interface IScenarioExecutor
    {
        public Task ExecuteAsync(Scenario scenario);
    }
}