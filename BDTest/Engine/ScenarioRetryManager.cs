using BDTest;
using BDTest.Exceptions;
using BDTest.Interfaces.Internal;
using BDTest.Reporters;
using BDTest.Test;

namespace BDTest.Engine;

internal class ScenarioRetryManager : IScenarioRetryManager
{
    public async Task CheckIfAlreadyExecuted(Scenario scenario, ExecutionCancellation? cancellation = null)
    {
        if (scenario.ShouldRetry)
        {
            await ResetData(scenario, cancellation);
            return;
        }
            
        if (scenario.AlreadyExecuted)
        {
            throw new AlreadyExecutedException("This scenario has already been executed");
        }

        scenario.AlreadyExecuted = true;
    }
        
    private async Task ResetData(Scenario scenario, ExecutionCancellation? cancellation)
    {
        SetRetryData(scenario);

        ResetStepData(scenario);

        scenario.Status = Status.NotRun;

        await RunRetryTestHooks(scenario, cancellation);

        await ConsoleReporter.WriteLine($"{Environment.NewLine}Retrying test...{Environment.NewLine}");
    }

    private async Task RunRetryTestHooks(Scenario scenario, ExecutionCancellation? cancellation)
    {
        try
        {
            var bdTestBase = scenario.BdTestBaseClass ?? throw new InvalidOperationException("The scenario has no test instance.");

            // Run TearDown Attributed Method
            await bdTestBase.RunMethodWithAttribute<BDTestRetryTearDownAttribute>(cancellation?.Token ?? default, Invoke);

            // Run Custom Test Hook In Base Class
            await Invoke(() => bdTestBase.OnBeforeRetry(cancellation?.Token ?? default));

            await Invoke(() => bdTestBase.RecreateContextOnRetryAsync(scenario.RetryCount).AsTask());

            // Run SetUp Attributed Method
            await bdTestBase.RunMethodWithAttribute<BDTestRetrySetUpAttribute>(cancellation?.Token ?? default, Invoke);
        }
        catch (Exception e) when (!(e is OperationCanceledException && scenario.ExecutionCancelled))
        {
            throw new ErrorOccurredDuringRetryActionException(e);
        }

        Task Invoke(Func<Task> action) => cancellation?.RunAsync(action) ?? action();
    }

    private static void ResetStepData(Scenario scenario)
    {
        foreach (var step in scenario.ExecutionSteps)
        {
            step.ResetData();
        }
    }

    private static void SetRetryData(Scenario scenario)
    {
        scenario.ShouldRetry = false;

        scenario.RetryCount++;
    }

}
