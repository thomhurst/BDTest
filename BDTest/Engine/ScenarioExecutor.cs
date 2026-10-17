using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Interfaces.Internal;
using BDTest.Output;
using BDTest.Reporters;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest.Engine;

internal class ScenarioExecutor : IScenarioExecutor
{
    private readonly IScenarioRetryManager _scenarioRetryManager;

    internal ScenarioExecutor(IScenarioRetryManager scenarioRetryManager)
    {
        _scenarioRetryManager = scenarioRetryManager;
    }

    public async Task ExecuteAsync(Scenario scenario)
    {
        // Completed steps and retry hooks can run inline. Iterate attempts so
        // repeated synchronous failures cannot grow the execution stack.
        do
        {
            await _scenarioRetryManager.CheckIfAlreadyExecuted(scenario).ConfigureAwait(false);
            await ExecuteAttemptAsync(scenario).ConfigureAwait(false);
        } while (scenario.ShouldRetry);
    }

    private async Task ExecuteAttemptAsync(Scenario scenario)
    {
        try
        {
            scenario.StartTime = DateTime.Now;

            TestOutputData.ClearCurrentTaskData();

            if (scenario.RetryCount == 0)
            {
                await WriteTestInformation(scenario).ConfigureAwait(false);
            }

            if (ShouldSkip(scenario))
            {
                scenario.Status = Status.Skipped;
                scenario.Steps.ForEach(step => step.Status = Status.Skipped);
                return;
            }

            ConsoleReporter.WriteLineToConsoleOnly($"--------------------------------------------------{Environment.NewLine}", ConsoleColor.Yellow);

            foreach (var step in scenario.Steps)
            {
                await step.Execute().ConfigureAwait(false);
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

            await ConsoleReporter.WriteLine($"{Environment.NewLine}Exception: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}", ConsoleColor.DarkRed).ConfigureAwait(false);

            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(scenario.Output))
            {
                ConsoleReporter.WriteLineToConsoleOnly($"{Environment.NewLine}--------------------------------------------------{Environment.NewLine}", ConsoleColor.Yellow);
            }


            if (!scenario.ShouldRetry)
            {
                foreach (var notRunStep in scenario.Steps.Where(step => step.Status == Status.Inconclusive))
                {
                    notRunStep.SetStepText();
                }

                ConsoleReporter.WriteLineToConsoleOnly($"Test Summary:{Environment.NewLine}");

                foreach (var step in scenario.Steps)
                {
                    ConsoleReporter.WriteLineToConsoleOnly($"{step.StepText} > [{step.Status}]");
                }

                ConsoleReporter.WriteLineToConsoleOnly($"{Environment.NewLine}--------------------------------------------------{Environment.NewLine}", ConsoleColor.Yellow);

                ConsoleReporter.WriteLineToConsoleOnly($"Test Result: {scenario.Status}");

                scenario.EndTime = DateTime.Now;
                scenario.TimeTaken = scenario.EndTime - scenario.StartTime;

                scenario.Output = string.Join(Environment.NewLine,
                    scenario.Steps.Where(step => !string.IsNullOrWhiteSpace(step.Output)).Select(step => step.Output));
            }
        }
    }

    private bool ShouldSkip(Scenario scenario)
    {
        return BDTestSettings.GlobalSkipTestRules.Rules.Any(x => x.Invoke(scenario));
    }

    private async Task WriteTestInformation(Scenario scenario)
    {
        await WriteStoryAndScenario(scenario).ConfigureAwait(false);

        await WriteCustomTestInformation(scenario).ConfigureAwait(false);

        TestOutputData.ClearCurrentTaskData();
    }

    private async Task WriteCustomTestInformation(Scenario scenario)
    {
        var scenarioCustomTestInformation = scenario.CustomTestInformation ?? Array.Empty<TestInformationAttribute>();

        if (!scenarioCustomTestInformation.Any())
        {
            return;
        }

        foreach (var testInformationAttribute in scenarioCustomTestInformation)
        {
            await ConsoleReporter.WriteLine(testInformationAttribute.Print()).ConfigureAwait(false);
        }

        await ConsoleReporter.WriteLine(Environment.NewLine).ConfigureAwait(false);
    }

    private async Task WriteStoryAndScenario(Scenario scenario)
    {
        await ConsoleReporter.WriteStory(scenario.StoryText).ConfigureAwait(false);

        await ConsoleReporter.WriteScenario(scenario.ScenarioText).ConfigureAwait(false);

        await ConsoleReporter.WriteLine(string.Empty).ConfigureAwait(false);
    }
}