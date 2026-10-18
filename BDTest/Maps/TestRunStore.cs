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
    private readonly Dictionary<string, List<(Action<Scenario> Publish, Action? Discard)>> ScenarioListeners = new();
    private readonly Dictionary<string, BufferedOutput> PendingStartupOutput = new();
    private readonly Dictionary<string, AttachmentExecution> AttachmentExecutions = new();

    private sealed class AttachmentExecution
    {
        internal bool Completed;
    }

    internal void BeginAttachmentExecution(string frameworkExecutionId)
    {
        lock (Gate)
        {
            // Preserve constructor writers, but never revive a completed execution's writers.
            if (AttachmentExecutions.TryGetValue(frameworkExecutionId, out var execution) && execution.Completed)
                AttachmentExecutions.Remove(frameworkExecutionId);
        }
    }

    // Constructor-created writers share the lifetime later completed by adapter teardown.
    internal Action GetAttachmentExecutionGuard(string frameworkExecutionId)
    {
        lock (Gate)
        {
            if (!AttachmentExecutions.TryGetValue(frameworkExecutionId, out var execution))
                AttachmentExecutions.Add(frameworkExecutionId, execution = new AttachmentExecution());
            return () =>
            {
                if (execution.Completed)
                    throw new InvalidOperationException("The attachment's test execution has already completed.");
            };
        }
    }

    internal void AppendStartupOutput(string frameworkExecutionId, char value)
    {
        lock (Gate)
        {
            GetStartupOutput(frameworkExecutionId).Append(value);
        }
    }

    internal void AppendStartupOutput(string frameworkExecutionId, string? text, bool appendNewLine)
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

    internal void ListenForScenario(string frameworkExecutionId, Action<Scenario> @delegate, Action? onDiscard = null)
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
                ScenarioListeners.Add(frameworkExecutionId, list = new List<(Action<Scenario> Publish, Action? Discard)>());
            }
            list.Add((@delegate, onDiscard));
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
            NotRun[builder.Guid ?? throw new InvalidOperationException("The builder has no ID.")] = builder;
        }
    }

    internal void AddScenario(Scenario scenario)
    {
        lock (Gate)
        {
            ThrowIfDisposed();
            var frameworkId = scenario.FrameworkTestId ?? throw new InvalidOperationException("The scenario has no execution ID.");
            var scenarioId = scenario.Guid ?? throw new InvalidOperationException("The scenario has no ID.");
            if (PendingStartupOutput.TryGetValue(frameworkId, out var output))
            {
                scenario.StartupOutput = output;
                PendingStartupOutput.Remove(frameworkId);
            }

            ScenariosByInternalId[scenarioId] = scenario;
            ScenariosByTestFrameworkId[frameworkId] = scenario;

            if (ScenarioListeners.TryGetValue(frameworkId, out var delegateList))
            {
                ScenarioListeners.Remove(frameworkId);
                foreach (var listener in delegateList)
                {
                    listener.Publish(scenario);
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
            foreach (var id in AttachmentExecutions.Where(pair => pair.Value.Completed).Select(pair => pair.Key).ToArray())
                AttachmentExecutions.Remove(id);
        }
    }

    internal void CompleteTest(string frameworkExecutionId)
    {
        lock (Gate)
        {
            PendingStartupOutput.Remove(frameworkExecutionId);
            if (!AttachmentExecutions.TryGetValue(frameworkExecutionId, out var execution))
                AttachmentExecutions.Add(frameworkExecutionId, execution = new AttachmentExecution());
            execution.Completed = true;
            if (ScenarioListeners.TryGetValue(frameworkExecutionId, out var listeners))
            {
                foreach (var listener in listeners) listener.Discard?.Invoke();
                ScenarioListeners.Remove(frameworkExecutionId);
            }
        }
    }
}
