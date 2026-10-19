using System.Runtime.ExceptionServices;
using System.Threading;
using BDTest.Output;
using BDTest.Test;

namespace BDTest.Engine;

// Bounds individual user calls, never the orchestration that mutates report results.
internal sealed class ExecutionCancellation : IDisposable
{
    internal static readonly TimeSpan GracePeriod = TimeSpan.FromMilliseconds(250);
    private readonly Scenario _scenario;
    private readonly Step? _step;
    private readonly ExecutionCancellation? _parent;
    private readonly CancellationTokenSource _source = new();
    private readonly CancellationTokenRegistration[] _registrations;
    private readonly TaskCompletionSource<bool> _signalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Task> _outstanding = new();
    private readonly ExecutionTimer _timer;
    private readonly TimeSpan? _timeout;
    private readonly object _gate = new();
    private Task? _callbacks;
    private Exception? _failure;

    internal CancellationToken Token => _source.Token;

    internal ExecutionCancellation(Scenario scenario, CancellationToken explicitToken, CancellationToken frameworkToken, bool useTimeout = true)
        : this(scenario, null, null, useTimeout ? scenario.Timeout : null, new[] { explicitToken, frameworkToken }) { }

    internal ExecutionCancellation(Scenario scenario, Step step, ExecutionCancellation parent)
        : this(scenario, step, parent, step.Timeout, new[] { parent.Token }) { }

    private ExecutionCancellation(Scenario scenario, Step? step, ExecutionCancellation? parent,
        TimeSpan? timeout, CancellationToken[] tokens)
    {
        _scenario = scenario;
        _step = step;
        _parent = parent;
        _timeout = timeout;
        _timer = scenario.ExecutionRun.Options.Clock.StartTimer();
        _registrations = tokens.Select(token => token.Register(() => Cancel())).ToArray();
    }

    private (TimeSpan? Remaining, ExecutionCancellation Owner) Budget
    {
        get
        {
            var remaining = _timeout - _timer.Elapsed;
            var parent = _parent?.Budget;
            return parent.HasValue && parent.Value.Remaining.HasValue &&
                (!remaining.HasValue || parent.Value.Remaining <= remaining)
                ? parent.Value : (remaining, this);
        }
    }

    private Exception Failure
    {
        get
        {
            if (_failure != null) return _failure;
            if (_parent?._failure != null) return _parent._failure;
            _scenario.ExecutionCancelled = true;
            return new OperationCanceledException("Scenario execution was cancelled by the caller or test framework.", Token);
        }
    }

    // Never run user cancellation callbacks on the caller's cancellation thread.
    private void Cancel(Exception? failure = null)
    {
        lock (_gate)
        {
            _failure ??= failure;
            if (_callbacks != null) return;
            _signalled.TrySetResult(true);
            _callbacks = Task.Run(() =>
            {
                using var started = Token.Register(() => _started.TrySetResult(true));
                try { _source.Cancel(); }
                catch (Exception) { /* Cancellation callback failures cannot replace the execution result. */ }
            });
        }
    }

    private Exception Expire(ExecutionCancellation owner)
    {
        var failure = new TimeoutException($"{(owner._step == null ? "Scenario" : "Step")} timed out after {owner._timeout}" +
            (_step == null ? "." : $" while executing '{_step.StepText}'."));
        _scenario.AttemptTimedOut = true;
        if (_step != null) _step.TimedOut = true;
        Cancel(failure);
        _parent?.Cancel(failure);
        return failure;
    }

    internal void ThrowIfCancelled()
    {
        if (_signalled.Task.IsCompleted || _parent?._signalled.Task.IsCompleted == true)
            ExceptionDispatchInfo.Capture(Failure).Throw();
    }

    internal async Task RunAsync(Func<Task> invoke, bool cleanup = false)
    {
        var budget = Budget;
        var cancelledAtStart = _signalled.Task.IsCompleted || _parent?._signalled.Task.IsCompleted == true;
        if (!cleanup)
        {
            ThrowIfCancelled();
            if (budget.Remaining <= TimeSpan.Zero) throw Expire(budget.Owner);
        }

        if (cleanup && budget.Remaining <= TimeSpan.Zero && !_signalled.Task.IsCompleted)
            Expire(budget.Owner);
        if (cleanup && (_signalled.Task.IsCompleted || _parent?._signalled.Task.IsCompleted == true))
        {
            Cancel(Failure);
            await _started.Task;
        }

        using var output = new ExecutionOutputScope(_scenario.ExecutionRun);
        using var delayCancellation = new CancellationTokenSource();
        // Preserve direct invocation and the caller's context without a deadline.
        // A deadline needs a boundary even for an async delegate's synchronous prefix.
        var delay = budget.Remaining.HasValue && budget.Remaining > TimeSpan.Zero
            ? Delay(budget.Remaining.Value, delayCancellation.Token) : Task.Delay(Timeout.Infinite, delayCancellation.Token);
        var execution = budget.Remaining.HasValue || (cleanup && _signalled.Task.IsCompleted)
            ? Task.Run(invoke) : InvokeAsync(invoke);
        try
        {
            // A user callback on the parent's token can block before our registration runs.
            // Observe its private signal independently of token callback dispatch.
            var winner = _parent == null
                ? await Task.WhenAny(execution, _signalled.Task, delay)
                : await Task.WhenAny(execution, _signalled.Task, _parent._signalled.Task, delay);
            if (winner == execution && !_signalled.Task.IsCompleted && _parent?._signalled.Task.IsCompleted != true)
            {
                await execution;
                // When both tasks completed before WhenAny observes them, the first argument wins.
                // A successful final callback still consumes its deadline in that tie.
                budget = Budget;
                if (!budget.Remaining.HasValue || budget.Remaining > TimeSpan.Zero) return;
                winner = delay;
            }

            var failure = winner == delay || budget.Remaining <= TimeSpan.Zero
                ? Expire(budget.Owner) : Failure;
            Cancel(failure);
            await _started.Task;
            // Include callbacks in the grace budget: they can also retain and mutate Context.
            var settled = Task.WhenAll(Observe(execution), _callbacks!);
            if (await Task.WhenAny(settled, Task.Delay(GracePeriod)) != settled)
            {
                _scenario.UncooperativeExecution = true;
                _outstanding.Add(settled);
                var diagnostic = "Cancellation was ignored beyond the 250 ms grace period; output capture detached and retries disabled.";
                _step?.Diagnostics?.Add(diagnostic);
                failure = failure is TimeoutException
                    ? new TimeoutException(failure.Message + " " + diagnostic)
                    : new OperationCanceledException(failure.Message + " " + diagnostic, Token);
            }
            else if (cleanup && cancelledAtStart)
            {
                await execution;
                return;
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        finally { delayCancellation.Cancel(); }
    }

    private Task Delay(TimeSpan time, CancellationToken token)
    {
#if NET8_0_OR_GREATER
        return Task.Delay(time, _scenario.ExecutionRun.Options.Clock.Provider, token);
#else
        return Task.Delay(time, token);
#endif
    }

    private static async Task InvokeAsync(Func<Task> invoke) => await invoke();
    private static async Task Observe(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception) { }
    }

    public void Dispose()
    {
        foreach (var registration in _registrations) registration.Dispose();
        if (_callbacks != null) _outstanding.Add(_callbacks);
        _ = DisposeWhenSettledAsync();
    }

    private async Task DisposeWhenSettledAsync()
    {
        await Task.WhenAll(_outstanding).ConfigureAwait(false);
        _source.Dispose();
    }
}
