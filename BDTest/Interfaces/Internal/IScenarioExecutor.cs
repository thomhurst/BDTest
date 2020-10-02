using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Output;
using BDTest.Reporters;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest.Interfaces.Internal
{
    public interface IScenarioExecutor
    {
        public Task ExecuteAsync(Scenario scenario);
    }

    public class ScenarioExecutor : IScenarioExecutor
    {
        public async Task ExecuteAsync(Scenario scenario)
        {
            await CheckIfAlreadyExecuted(scenario).ConfigureAwait(false);
            
            await Task.Run(async () =>
            {
                try
                {
                    scenario.StartTime = DateTime.Now;
                    
                    TestOutputData.ClearCurrentTaskData();

                    if (scenario.RetryCount == 0)
                    {
                        WriteTestInformation(scenario);
                    }

                    foreach (var step in scenario.Steps)
                    {
                        await step.Execute();
                    }

                    scenario.Status = Status.Passed;
                }
                catch (NotImplementedException)
                {
                    scenario.Status = Status.NotImplemented;
                    throw;
                }
                catch (Exception e) when (BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(e.GetType()))
                {
                    scenario.Status = Status.Passed;
                    throw;
                }
                catch (Exception e) when (BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(e.GetType()))
                {
                    scenario.Status = Status.Inconclusive;
                    throw;
                }
                catch (Exception e)
                {
                    var validRetryRules = BDTestSettings.RetryTestRules.Rules.Where(rule => rule.Condition(e)).ToList();
                    if (validRetryRules.Any() && scenario.RetryCount < validRetryRules.Max(x => x.RetryLimit))
                    {
                        scenario.ShouldRetry = true;
                        return;
                    }
                    
                    scenario.Status = Status.Failed;
                    
                    ConsoleReporter.WriteLine($"{Environment.NewLine}Exception: {e.StackTrace}{Environment.NewLine}");

                    throw;
                }
                finally
                {
                    if (scenario.ShouldRetry)
                    {
                        await ExecuteAsync(scenario);
                    }
                    else
                    {
                        foreach (var notRunStep in scenario.Steps.Where(step => step.Status == Status.Inconclusive))
                        {
                            notRunStep.SetStepText();
                        }

                        ConsoleReporter.WriteLine($"{Environment.NewLine}Test Summary:{Environment.NewLine}");

                        scenario.Steps.ForEach(step => ConsoleReporter.WriteLine($"{step.StepText} > [{step.Status}]"));

                        ConsoleReporter.WriteLine($"{Environment.NewLine}Test Result: {scenario.Status}{Environment.NewLine}");

                        scenario.EndTime = DateTime.Now;
                        scenario.TimeTaken = scenario.EndTime - scenario.StartTime;

                        scenario.Output = string.Join(Environment.NewLine,
                            scenario.Steps.Where(step => !string.IsNullOrWhiteSpace(step.Output)).Select(step => step.Output));
                    }
                }
            });
        }
        
        private async Task CheckIfAlreadyExecuted(Scenario scenario)
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
            if (IsSuperClassOfAbstractContextBDTestBase(scenario))
            {
                scenario._testDetails.BdTestBase.GetType().GetMethod("RecreateContextOnRetry", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(scenario._testDetails.BdTestBase, Array.Empty<object>());
            }
        }
        
        private bool IsSuperClassOfAbstractContextBDTestBase(Scenario scenario)
        {
            var type = scenario._testDetails.BdTestBase.GetType();
            do
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AbstractContextBDTestBase<>))
                {
                    return true;
                }

                type = type.BaseType;
            } while (type != null);

            return false;
        }
        
        private void WriteTestInformation(Scenario scenario)
        {
            WriteStoryAndScenario(scenario);
            
            WriteCustomTestInformation(scenario);
            
            TestOutputData.ClearCurrentTaskData();
        }

        private void WriteCustomTestInformation(Scenario scenario)
        {
            foreach (var testInformationAttribute in scenario.CustomTestInformation ?? Array.Empty<TestInformationAttribute>())
            {
                ConsoleReporter.WriteLine(testInformationAttribute.Print());
            }
            
            ConsoleReporter.WriteLine(Environment.NewLine);
        }

        private void WriteStoryAndScenario(Scenario scenario)
        {
            ConsoleReporter.WriteStory(scenario.StoryText);
            
            ConsoleReporter.WriteScenario(scenario.ScenarioText);
            
            ConsoleReporter.WriteLine(Environment.NewLine);
        }
    }
}