using System;
using System.Reflection;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Reporters;
using BDTest.Test;

namespace BDTest.Interfaces.Internal
{
    public interface IScenarioRetryManager
    {
        Task CheckIfAlreadyExecuted(Scenario scenario);
    }

    public class ScenarioRetryManager : IScenarioRetryManager
    {
        private readonly ITypeMatcher _typeMatcher;

        public ScenarioRetryManager(ITypeMatcher typeMatcher)
        {
            _typeMatcher = typeMatcher;
        }
        
        public async Task CheckIfAlreadyExecuted(Scenario scenario)
        {
            if (scenario.ShouldRetry)
            {
                await SetRetryValues(scenario).ConfigureAwait(false);
                return;
            }
            
            if (scenario._alreadyExecuted)
            {
                throw new AlreadyExecutedException("This scenario has already been executed");
            }

            scenario._alreadyExecuted = true;
        }
        
        private async Task SetRetryValues(Scenario scenario)
        {
            scenario.ShouldRetry = false;
            
            scenario.RetryCount++;
            
            foreach (var step in scenario.Steps)
            {
                step.ResetData();
            }

            scenario.Status = Status.Inconclusive;

            try
            {
                await scenario._testDetails.BdTestBase.RunMethodWithAttribute<BDTestRetryTearDownAttribute>();
                
                await scenario._testDetails.BdTestBase.OnBeforeRetry();

                ResetContext(scenario);

                await scenario._testDetails.BdTestBase.RunMethodWithAttribute<BDTestRetrySetUpAttribute>();
            }
            catch (Exception e)
            {
                throw new ErrorOccurredDuringRetryActionException(e);
            }

            ConsoleReporter.WriteLine("\nRetrying test...\n");
        }
        
        private void ResetContext(Scenario scenario)
        {
            if (_typeMatcher.IsSuperClassOfAbstractContextBDTestBase(scenario._testDetails.BdTestBase))
            {
                scenario._testDetails.BdTestBase.GetType().GetMethod("RecreateContextOnRetry", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(scenario._testDetails.BdTestBase, Array.Empty<object>());
            }
        }
    }
}