using System;
using System.Linq;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Interfaces.Internal;
using BDTest.Output;
using BDTest.Reporters;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest.Engine
{
    internal class ScenarioExecutor : IScenarioExecutor
    {
        private readonly IScenarioRetryManager _scenarioRetryManager;

        internal ScenarioExecutor(IScenarioRetryManager scenarioRetryManager)
        {
            _scenarioRetryManager = scenarioRetryManager;
        }
        
        public async Task ExecuteAsync(Scenario scenario)
        {
            await _scenarioRetryManager.CheckIfAlreadyExecuted(scenario).ConfigureAwait(false);
            
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
                    
                    if (ShouldSkip(scenario))
                    {
                        scenario.Status = Status.Skipped;
                        return;
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
                    var validRetryRules = BDTestSettings.GlobalRetryTestRules.Rules.Where(rule => rule.Condition(e)).ToList();
                    if (validRetryRules.Any() && scenario.RetryCount < validRetryRules.Max(x => x.RetryLimit))
                    {
                        scenario.ShouldRetry = true;
                        return;
                    }

                    var bdTestRetryAttribute = RetryAttributeHelper.GetBDTestRetryAttribute(scenario);
                    if (bdTestRetryAttribute != null && scenario.RetryCount < bdTestRetryAttribute.Count)
                    {
                        scenario.ShouldRetry = true;
                        return;
                    }
                    
                    scenario.Status = Status.Failed;
                    
                    ConsoleReporter.WriteLine($"{Environment.NewLine}Exception: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");

                    throw;
                }
                finally
                {
                    if (scenario.ShouldRetry)
                    {
                        await ExecuteAsync(scenario).ConfigureAwait(false);
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

        private bool ShouldSkip(Scenario scenario)
        {
            return BDTestSettings.GlobalSkipTestRules.Rules.Any(x => x.Invoke(scenario));
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