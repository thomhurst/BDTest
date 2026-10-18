using System.Runtime.ExceptionServices;
using System.Threading;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Test;

namespace BDTest.Engine;

internal static class ScenarioHookExecutor
{
    internal static async Task ExecuteAsync(Scenario scenario, Func<Task> execute, Func<Func<Task>, Task> executeInScope)
    {
        var test = scenario.BdTestBaseClass;
        // Keep the legacy event subscriptions stable throughout this attempt.
        var beforeHooks = scenario.Run.Options.BeforeScenario;
        var afterHooks = scenario.Run.Options.AfterScenario;
        var cancellation = new CancellationTokenSource();
        TaskCompletionSource<bool> cleanupFinished = null;
        try
        {
            var context = test?.CreateScenarioHookContext(scenario, cancellation.Token)
                ?? new ScenarioHookContext(scenario, null, cancellation.Token);
            Exception executionException = null;
            try
            {
                // Complete the adapter's assertion scope before exposing the result to cleanup.
                await executeInScope(async () =>
                {
                    if (beforeHooks != null)
                        foreach (Func<ScenarioHookContext, Task> hook in beforeHooks.GetInvocationList())
                            await hook(context);
                    if (test != null) await test.OnBeforeScenario(context);
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
                if (scenario.TimedOut)
                {
                    var cancellationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    // Registered last, this signal runs before user callbacks. Cleanup can
                    // observe cancellation without waiting for callbacks that need cleanup.
                    cancellation.Token.Register(() => cancellationStarted.TrySetResult(true));
                    cleanupFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _ = Step.CancelAndObserve(cleanupFinished.Task, cancellation);
                    cancellation = null; // The background cancellation now owns disposal.
                    await cancellationStarted.Task;
                }

                if (test != null) await InvokeAfter(test.OnAfterScenario);
                if (afterHooks != null)
                    foreach (Func<ScenarioHookContext, Task> hook in afterHooks.GetInvocationList())
                        await InvokeAfter(hook);
            }
            // Throw only after every cleanup handler has had a chance to run.
            if (executionException != null) ExceptionDispatchInfo.Capture(executionException).Throw();

            async Task InvokeAfter(Func<ScenarioHookContext, Task> hook)
            {
                try { await executeInScope(() => hook(context)); }
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
                scenario.Status = ExceptionOutcomeClassifier.Classify(exception, scenario.Run.Options, scenario.TimedOut);
                if (scenario.Status is Status.Failed or Status.NotImplemented)
                    scenario.ExecutionException = new ExceptionWrapper(exception);
                if (scenario.Status == Status.Skipped)
                {
                    foreach (var step in scenario.Steps.Where(step => step.Status == Status.NotRun))
                    {
                        step.SetStepText();
                        step.Status = Status.Skipped;
                    }
                }
            }
        }
        finally
        {
            // Keep the source usable until both callbacks and cleanup have finished.
            cleanupFinished?.TrySetResult(true);
            cancellation?.Dispose();
        }
    }
}
