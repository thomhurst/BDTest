using System.Runtime.ExceptionServices;
using BDTest.Helpers;
using BDTest.Test;

namespace BDTest.Engine;

internal static class ScenarioHookExecutor
{
    internal static async Task ExecuteAsync(Scenario scenario, Func<Task> execute, Func<Func<Task>, Task> executeInScope, ExecutionCancellation cancellation)
    {
        var test = scenario.BdTestBaseClass;
        // Use the owning run's frozen hook registrations for this attempt.
        var beforeHooks = scenario.ExecutionRun.Options.BeforeScenario;
        var afterHooks = scenario.ExecutionRun.Options.AfterScenario;
        ScenarioHookContext? context = null;
        Exception? executionException = null;
        try
        {
            // Complete the adapter's assertion scope before exposing the result to cleanup.
            await executeInScope(async () =>
            {
                if (test != null) await cancellation.RunAsync(() => test.InitializeExecutionContextAsync().AsTask());
                context = test?.CreateScenarioHookContext(scenario, cancellation.Token)
                    ?? new ScenarioHookContext(scenario, null, cancellation.Token);
                if (beforeHooks != null)
                    foreach (Func<ScenarioHookContext, Task> hook in beforeHooks.GetInvocationList())
                        await cancellation.RunAsync(() => hook(context!.WithSnapshot(scenario)));
                if (test != null) await cancellation.RunAsync(() => test.OnBeforeScenario(context.WithSnapshot(scenario)));
                await execute();
            });
        }
        catch (Exception exception)
        {
            executionException = exception;
            SetResult(exception);
        }
        finally
        {
            var hadFailure = executionException != null;
            var timedOut = scenario.AttemptTimedOut;
            if (context != null)
            {
                if (test != null) await InvokeAfter(test.OnAfterScenario);
                if (afterHooks != null)
                    foreach (Func<ScenarioHookContext, Task> hook in afterHooks.GetInvocationList())
                        await InvokeAfter(hook);
            }
            if (hadFailure) scenario.AttemptTimedOut = timedOut;
        }
        // Throw only after every cleanup handler has had a chance to run.
        if (executionException != null) ExceptionDispatchInfo.Capture(executionException).Throw();

        async Task InvokeAfter(Func<ScenarioHookContext, Task> hook)
        {
            try { await executeInScope(() => cancellation.RunAsync(() => hook(context!.WithSnapshot(scenario)), cleanup: true)); }
            catch (Exception exception)
            {
                if (executionException == null)
                {
                    executionException = exception;
                    SetResult(exception);
                }
                await Console.Out.WriteLineAsync($"After-scenario hook failed: {exception}");
            }
        }

        void SetResult(Exception exception)
        {
            scenario.Status = ExceptionOutcomeClassifier.Classify(exception, scenario.ExecutionRun.Options, scenario.TimedOut || scenario.ExecutionCancelled);
            if (scenario.Status is Status.Failed or Status.NotImplemented)
                scenario.ExecutionException = new ExceptionWrapper(exception);
            if (scenario.Status == Status.Skipped)
            {
                foreach (var step in scenario.ExecutionSteps.Where(step => step.Status == Status.NotRun))
                {
                    step.SetStepText();
                    step.Status = Status.Skipped;
                }
            }
        }
    }
}
