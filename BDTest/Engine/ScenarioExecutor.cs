using System.Diagnostics;
using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Helpers;
using BDTest.Interfaces.Internal;
using BDTest.Output;
using BDTest.Reporters;
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
        try
        {
            await _scenarioRetryManager.CheckIfAlreadyExecuted(scenario);

            // Completed steps and retry hooks can run inline. Iterate attempts so
            // repeated synchronous failures cannot grow the execution stack.
            // Capture the caller's synchronization context for subsequent steps and retries.
            do
            {
                await ExecuteAttemptAsync(scenario);
            } while (scenario.ShouldRetry);

            // Notify after finalizing the scenario, outside retry/exception classification.
            // For example, MSTest uses an inconclusive exception to signal a runtime skip.
            if (scenario.Status == Status.Skipped && scenario.OutlineId == null)
            {
                scenario.BdTestBaseClass?.NotifyScenarioSkipped("BDTest scenario was skipped.");
            }
        }
        finally
        {
            // Keep closures and the fixture alive through retries and text
            // formatting, but release them even when a retry hook fails.
            scenario.ReleaseRuntimeReferences();
        }
    }

    private async Task ExecuteAttemptAsync(Scenario scenario)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception attemptException = null;
        var preparingRetry = scenario.ShouldRetry;
        try
        {
            scenario.StartTime = DateTime.UtcNow;
            if (preparingRetry)
            {
                await _scenarioRetryManager.CheckIfAlreadyExecuted(scenario);
                preparingRetry = false;
            }
            scenario.Status = Status.Pending;

            TestOutputData.ClearCurrentTaskData();

            if (scenario.RetryCount == 0)
            {
                await WriteTestInformation(scenario);
            }

            if (ShouldSkip(scenario))
            {
                scenario.Status = Status.Skipped;
                scenario.Steps.ForEach(step =>
                {
                    if (scenario.OutlineId != null) step.SetStepText();
                    step.Status = Status.Skipped;
                });
                return;
            }

            ConsoleReporter.WriteLineToConsoleOnly($"--------------------------------------------------{Environment.NewLine}", ConsoleColor.Yellow);

            var stepBudget = scenario.Timeout.HasValue ? Stopwatch.StartNew() : null;
            foreach (var step in scenario.Steps)
            {
                await step.Execute(scenario.BdTestBaseClass, scenario.Timeout, scenario.Timeout - stepBudget?.Elapsed);
            }

            scenario.Status = scenario.Steps.Count > 0 && scenario.Steps.All(step => step.Status == Status.Skipped)
                ? Status.Skipped
                : Status.Passed;
        }
        catch (ErrorOccurredDuringRetryActionException e) when (preparingRetry)
        {
            // Hook failures end the retry even if the underlying exception has a custom outcome.
            attemptException = e.InnerException ?? e;
            scenario.Status = Status.Failed;
            scenario.ShouldRetry = false;
            scenario.ExecutionException = new ExceptionWrapper(attemptException);

            await ConsoleReporter.WriteLine($"{Environment.NewLine}Exception: {attemptException.Message}{Environment.NewLine}{attemptException.StackTrace}{Environment.NewLine}", ConsoleColor.DarkRed);
            throw;
        }
        catch (NotImplementedException e)
        {
            attemptException = e;
            scenario.Status = Status.NotImplemented;
            throw;
        }
        catch (Exception e) when (!scenario.TimedOut && scenario.Run.Options.SuccessExceptionTypes.Contains(e.GetType()))
        {
            attemptException = e;
            scenario.Status = Status.Passed;
            throw;
        }
        catch (Exception e) when (!scenario.TimedOut && scenario.Run.Options.SkipExceptionTypes.Contains(e.GetType()))
        {
            attemptException = e;
            scenario.Status = Status.Skipped;
            foreach (var step in scenario.Steps.Where(step => step.Status == Status.NotRun))
            {
                step.SetStepText();
                step.Status = Status.Skipped;
            }
            throw;
        }
        catch (Exception e) when (!scenario.TimedOut && scenario.Run.Options.InconclusiveExceptionTypes.Contains(e.GetType()))
        {
            attemptException = e;
            scenario.Status = Status.Inconclusive;
            throw;
        }
        catch (Exception e)
        {
            attemptException = e;
            scenario.Status = Status.Failed;

            var validRetryRules = scenario.Run.Options.RetryRules.Where(rule => rule.Condition(e)).ToList();
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

            await ConsoleReporter.WriteLine($"{Environment.NewLine}Exception: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}", ConsoleColor.DarkRed);

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
                foreach (var notRunStep in scenario.Steps.Where(step => step.Status == Status.NotRun))
                {
                    notRunStep.SetStepText();
                }

                ConsoleReporter.WriteLineToConsoleOnly($"Test Summary:{Environment.NewLine}");

                foreach (var step in scenario.Steps)
                {
                    ConsoleReporter.WriteLineToConsoleOnly($"{step.StepText} > [{step.Status}{(step.TimedOut ? " (Timed out)" : string.Empty)}]");
                }

                ConsoleReporter.WriteLineToConsoleOnly($"{Environment.NewLine}--------------------------------------------------{Environment.NewLine}", ConsoleColor.Yellow);

                ConsoleReporter.WriteLineToConsoleOnly($"Test Result: {scenario.Status}");

                scenario.Output = string.Join(Environment.NewLine,
                    scenario.Steps.Where(step => !string.IsNullOrWhiteSpace(step.Output)).Select(step => step.Output));
            }

            var endTime = DateTime.UtcNow;
            stopwatch.Stop();
            scenario.Attempts.Add(new ScenarioAttempt(scenario.Attempts.Count + 1, scenario.Status,
                attemptException, scenario.StartTime, endTime, stopwatch.Elapsed));

            if (!scenario.ShouldRetry)
            {
                scenario.EndTime = endTime;
                scenario.TimeTaken = stopwatch.Elapsed;
            }
        }
    }

    private bool ShouldSkip(Scenario scenario)
    {
        return scenario.Run.Options.SkipTestRules.Any(x => x.Invoke(scenario));
    }

    private async Task WriteTestInformation(Scenario scenario)
    {
        await WriteStoryAndScenario(scenario);

        await WriteCustomTestInformation(scenario);

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
            await ConsoleReporter.WriteLine(testInformationAttribute.Print());
        }

        await ConsoleReporter.WriteLine(Environment.NewLine);
    }

    private async Task WriteStoryAndScenario(Scenario scenario)
    {
        await ConsoleReporter.WriteStory(scenario.StoryText);

        await ConsoleReporter.WriteScenario(scenario.ScenarioText);

        await ConsoleReporter.WriteLine(string.Empty);
    }
}
