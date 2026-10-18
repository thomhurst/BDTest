using System.Collections.Concurrent;
using BDTest.Test;
using BDTest.Output;

namespace BDTest.Maps;

internal sealed class TestRunStore
{
    internal ConcurrentDictionary<string, BuildableTest> NotRun { get; } = new();
    internal ConcurrentDictionary<string, Scenario> ScenariosByInternalId { get; } = new();
    internal ConcurrentDictionary<string, Scenario> ScenariosByTestFrameworkId { get; } = new();
        
    // Registration, pending writes, listeners and completion must use the same gate.
    internal object Gate { get; } = new();
    private readonly Dictionary<string, List<Action<Scenario>>> ScenarioListeners = new();
    private readonly Dictionary<string, BufferedOutput> PendingStartupOutput = new();

    internal void AppendStartupOutput(string frameworkExecutionId, char value)
    {
        lock (Gate)
        {
            GetStartupOutput(frameworkExecutionId).Append(value);
        }
    }

    internal void AppendStartupOutput(string frameworkExecutionId, string text, bool appendNewLine)
    {
        lock (Gate)
        {
            GetStartupOutput(frameworkExecutionId).Append(text, appendNewLine);
        }
    }

    // Caller holds Gate, so a pending buffer cannot be detached during an append.
    private BufferedOutput GetStartupOutput(string frameworkExecutionId)
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

    internal void ListenForScenario(string frameworkExecutionId, Action<Scenario> @delegate)
    {
        lock (Gate)
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

    internal bool IsDisposed { get; set; }

    internal void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(BDTestRun));
    }

    internal void RegisterPending(BuildableTest builder)
    {
        lock (Gate)
        {
            ThrowIfDisposed();
            NotRun[builder.Guid] = builder;
        }
    }

    internal void AddScenario(Scenario scenario)
    {
        lock (Gate)
        {
            ThrowIfDisposed();
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

    internal void Clear()
    {
        lock (Gate)
        {
            ScenariosByInternalId.Clear();
            ScenariosByTestFrameworkId.Clear();
            NotRun.Clear();
            ScenarioListeners.Clear();
            PendingStartupOutput.Clear();
        }
    }

    internal void CompleteTest(string frameworkExecutionId)
    {
        lock (Gate)
        {
            PendingStartupOutput.Remove(frameworkExecutionId);
            ScenarioListeners.Remove(frameworkExecutionId);
        }
    }
}

