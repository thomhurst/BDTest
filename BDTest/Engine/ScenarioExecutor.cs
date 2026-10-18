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
        var clock = scenario.Run.Options.Clock;
        var timer = clock.StartTimer();
        Exception attemptException = null;
        Action reportTerminalFailure = null;
        var scopeFailed = false;
        var preparingRetry = scenario.ShouldRetry;
        try
        {
            scenario.StartTime = clock.UtcNow;
            if (preparingRetry)
            {
                await _scenarioRetryManager.CheckIfAlreadyExecuted(scenario);
                preparingRetry = false;
            }
            scenario.Status = Status.Pending;
            scenario.ExecutionException = null;

            TestOutputData.ClearCurrentTaskData();

            if (scenario.RetryCount == 0)
            {
                await WriteTestInformation(scenario);
            }

            ConsoleReporter.WriteLineToConsoleOnly($"--------------------------------------------------{Environment.NewLine}");

            await ScenarioHookExecutor.ExecuteAsync(scenario, ExecuteScenarioAsync, ExecuteInScopeAsync);

            async Task ExecuteInScopeAsync(Func<Task> execute)
            {
                Action diagnostics = null;
                try
                {
                    if (scenario.BdTestBaseClass is { } test)
                        await test.ExecuteScenarioStepsAsync(execute, report => diagnostics = report);
                    else
                        await execute();
                }
                catch
                {
                    // Cleanup diagnostics must not replace the original scope's failure.
                    if (!scopeFailed) reportTerminalFailure = diagnostics;
                    scopeFailed = true;
                    throw;
                }
            }

            async Task ExecuteScenarioAsync()
            {
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

                var stepBudget = scenario.Timeout.HasValue ? clock.StartTimer() : (ExecutionTimer?)null;
                foreach (var step in scenario.Steps)
                {
                    await step.Execute(scenario.BdTestBaseClass, scenario.Timeout, scenario.Timeout - stepBudget?.Elapsed, scenario);
                }

                scenario.Status = scenario.Steps.Count > 0 && scenario.Steps.All(step => step.Status == Status.Skipped)
                    ? Status.Skipped
                    : Status.Passed;
            }
        }
        catch (ErrorOccurredDuringRetryActionException e) when (preparingRetry)
        {
            // Hook failures end the retry even if the underlying exception has a custom outcome.
            attemptException = e.InnerException ?? e;
            scenario.Status = Status.Failed;
            scenario.ShouldRetry = false;
            scenario.ExecutionException = new ExceptionWrapper(attemptException);

            await ConsoleReporter.WriteLine($"{Environment.NewLine}Exception: {attemptException.Message}{Environment.NewLine}{attemptException.StackTrace}{Environment.NewLine}");
            throw;
        }
        catch (Exception e)
        {
            attemptException = e;
            scenario.Status = ExceptionOutcomeClassifier.Classify(e, scenario.Run.Options, scenario.TimedOut);
            if (scenario.Status is Status.Failed or Status.NotImplemented)
                scenario.ExecutionException ??= new ExceptionWrapper(e);
            if (scenario.Status == Status.Skipped)
            {
                foreach (var step in scenario.Steps.Where(step => step.Status == Status.NotRun))
                {
                    step.SetStepText();
                    step.Status = Status.Skipped;
                }
            }
            if (scenario.Status != Status.Failed) throw;

            var validRetryRules = scenario.Run.Options.RetryRules.Where(rule => rule.Condition(e)).ToList();
            if (validRetryRules.Any() && scenario.RetryCount < validRetryRules.Max(x => x.RetryLimit))
            {
                scenario.ShouldRetry = true;
                return;
            }

            if (scenario.TestMetadata?.RetryCount is int retryCount && scenario.RetryCount < retryCount)
            {
                scenario.ShouldRetry = true;
                return;
            }

            await ConsoleReporter.WriteLine($"{Environment.NewLine}Exception: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}");

            // Keep the original exception for retry predicates. Only terminal failures
            // publish additional diagnostics captured by a framework adapter.
            reportTerminalFailure?.Invoke();
            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(scenario.Output))
            {
                ConsoleReporter.WriteLineToConsoleOnly($"{Environment.NewLine}--------------------------------------------------{Environment.NewLine}");
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

                ConsoleReporter.WriteLineToConsoleOnly($"{Environment.NewLine}--------------------------------------------------{Environment.NewLine}");

                ConsoleReporter.WriteLineToConsoleOnly($"Test Result: {scenario.Status}");

                scenario.Output = string.Join(Environment.NewLine,
                    scenario.Steps.Select(step => step.Output)
                        .Concat(new[] { TestOutputData.Instance.ToString().Trim() })
                        .Where(output => !string.IsNullOrWhiteSpace(output)));
                TestOutputData.ClearCurrentTaskData();
            }

            var endTime = clock.UtcNow;
            var elapsed = timer.Elapsed;
            scenario.Attempts.Add(new ScenarioAttempt(scenario.Attempts.Count + 1, scenario.Status,
                attemptException, scenario.StartTime, endTime, elapsed));

            if (!scenario.ShouldRetry)
            {
                scenario.EndTime = endTime;
                scenario.TimeTaken = elapsed;
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
