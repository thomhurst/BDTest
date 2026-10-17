using System.Collections.Concurrent;
using BDTest.Test;
using BDTest.Output;

namespace BDTest.Maps;

internal static class TestHolder
{
    internal static readonly string CurrentReportId = Guid.NewGuid().ToString("N");
    internal static ConcurrentDictionary<string, BuildableTest> NotRun { get; } = new();
    internal static ConcurrentDictionary<string, Scenario> ScenariosByInternalId { get; } = new();
    internal static ConcurrentDictionary<string, Scenario> ScenariosByTestFrameworkId { get; } = new();
        
    // Registration, pending writes, listeners and completion must use the same gate.
    private static readonly object ScenarioLock = new();
    private static readonly Dictionary<string, List<Action<Scenario>>> ScenarioListeners = new();
    private static readonly Dictionary<string, BufferedOutput> PendingStartupOutput = new();

    internal static void AppendStartupOutput(string frameworkExecutionId, char value)
    {
        lock (ScenarioLock)
        {
            GetStartupOutput(frameworkExecutionId).Append(value);
        }
    }

    internal static void AppendStartupOutput(string frameworkExecutionId, string text, bool appendNewLine)
    {
        lock (ScenarioLock)
        {
            GetStartupOutput(frameworkExecutionId).Append(text, appendNewLine);
        }
    }

    // Caller holds ScenarioLock, so a pending buffer cannot be detached during an append.
    private static BufferedOutput GetStartupOutput(string frameworkExecutionId)
    {
        if (ScenariosByTestFrameworkId.TryGetValue(frameworkExecutionId, out var scenario))
        {
            return scenario.StartupOutput;
        }

        if (!PendingStartupOutput.TryGetValue(frameworkExecutionId, out var output))
        {
            PendingStartupOutput.Add(frameworkExecutionId, output = new BufferedOutput());
        }
        return output;
    }

    internal static void ListenForScenario(string frameworkExecutionId, Action<Scenario> @delegate)
    {
        lock (ScenarioLock)
        {
            if (ScenariosByTestFrameworkId.TryGetValue(frameworkExecutionId, out var scenario))
            {
                @delegate(scenario);
                return;
            }

            if (!ScenarioListeners.TryGetValue(frameworkExecutionId, out var list))
            {
                ScenarioListeners.Add(frameworkExecutionId, list = new List<Action<Scenario>>());
            }
            list.Add(@delegate);
        }
    }

    internal static void AddScenario(Scenario scenario)
    {
        lock (ScenarioLock)
        {
            if (PendingStartupOutput.TryGetValue(scenario.FrameworkTestId, out var output))
            {
                scenario.StartupOutput = output;
                PendingStartupOutput.Remove(scenario.FrameworkTestId);
            }

            ScenariosByInternalId[scenario.Guid] = scenario;
            ScenariosByTestFrameworkId[scenario.FrameworkTestId] = scenario;

            if (ScenarioListeners.TryGetValue(scenario.FrameworkTestId, out var delegateList))
            {
                ScenarioListeners.Remove(scenario.FrameworkTestId);
                foreach (var @delegate in delegateList)
                {
                    @delegate(scenario);
                }
            }
        }
    }

    internal static void Clear()
    {
        lock (ScenarioLock)
        {
            ScenariosByInternalId.Clear();
            ScenariosByTestFrameworkId.Clear();
            NotRun.Clear();
            ScenarioListeners.Clear();
            PendingStartupOutput.Clear();
        }
    }

    internal static void CompleteTest(string frameworkExecutionId)
    {
        lock (ScenarioLock)
        {
            PendingStartupOutput.Remove(frameworkExecutionId);
            ScenarioListeners.Remove(frameworkExecutionId);
        }
    }
}
