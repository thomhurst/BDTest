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
            
            if (scenario.AlreadyExecuted)
            {
                throw new AlreadyExecutedException("This scenario has already been executed");
            }

            scenario.AlreadyExecuted = true;
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
                var bdTestBase = scenario.TestDetails.BdTestBase;
                
                // Run TearDown Attributed Method
                await bdTestBase.RunMethodWithAttribute<BDTestRetryTearDownAttribute>();
                
                // Run Custom Test Hook In Base Class
                await bdTestBase.OnBeforeRetry();
                
                ResetContext(scenario);

                // Run SetUp Attributed Method
                await bdTestBase.RunMethodWithAttribute<BDTestRetrySetUpAttribute>();
            }
            catch (Exception e)
            {
                throw new ErrorOccurredDuringRetryActionException(e);
            }

            ConsoleReporter.WriteLine("\nRetrying test...\n");
        }
        
        private void ResetContext(Scenario scenario)
        {
            if (_typeMatcher.IsSuperClassOfAbstractContextBDTestBase(scenario.TestDetails.BdTestBase))
            {
                scenario.TestDetails.BdTestBase.GetType().GetMethod("RecreateContextOnRetry", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(scenario.TestDetails.BdTestBase, Array.Empty<object>());
            }
        }
    }
}