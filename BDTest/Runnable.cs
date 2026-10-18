using System.Runtime.CompilerServices;
using System.Threading;
using BDTest.Helpers;

namespace BDTest;

internal sealed class Runnable
{
    private readonly Func<CancellationToken, Task> _run;
    internal Delegate StepDelegate { get; }
    internal StepTextDefinition Definition { get; }

    private Runnable(Delegate step, Func<CancellationToken, Task> run, string argumentExpression,
        string callerFile, int callerLine, bool acceptsCancellationToken)
    {
        StepDelegate = step ?? throw new ArgumentNullException(nameof(step));
        _run = run;
        Definition = new StepTextDefinition(step, argumentExpression, callerFile, callerLine, acceptsCancellationToken);
    }

    [OverloadResolutionPriority(-1)]
    internal Runnable(Action step, string argumentExpression = null, string callerFile = null, int callerLine = 0)
        : this(step, _ => { step(); return Task.CompletedTask; }, argumentExpression, callerFile, callerLine, false) { }

    [OverloadResolutionPriority(1)]
    internal Runnable(Func<Task> step, string argumentExpression = null, string callerFile = null, int callerLine = 0)
        : this(step, _ => step(), argumentExpression, callerFile, callerLine, false) { }

    [OverloadResolutionPriority(1)]
    internal Runnable(Func<CancellationToken, Task> step, string argumentExpression = null, string callerFile = null, int callerLine = 0)
        : this(step, token => step(token), argumentExpression, callerFile, callerLine, true) { }

    internal Runnable(Func<ValueTask> step, string argumentExpression = null, string callerFile = null, int callerLine = 0)
        : this(step, async _ => await step().ConfigureAwait(false), argumentExpression, callerFile, callerLine, false) { }

    internal Runnable(Func<CancellationToken, ValueTask> step, string argumentExpression = null, string callerFile = null, int callerLine = 0)
        : this(step, async token => await step(token).ConfigureAwait(false), argumentExpression, callerFile, callerLine, true) { }

    internal static Runnable FromResult<T>(Func<ValueTask<T>> step, string argumentExpression, string callerFile, int callerLine)
        => new(step, async _ => { await step().ConfigureAwait(false); }, argumentExpression, callerFile, callerLine, false);

    internal static Runnable FromResult<T>(Func<CancellationToken, ValueTask<T>> step, string argumentExpression, string callerFile, int callerLine)
        => new(step, async token => { await step(token).ConfigureAwait(false); }, argumentExpression, callerFile, callerLine, true);

    internal Task Run(CancellationToken cancellationToken = default) => _run(cancellationToken);
}
