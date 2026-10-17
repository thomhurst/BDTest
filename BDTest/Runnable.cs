using System.Linq.Expressions;
using System.Reflection;
using BDTest.Reporters;

namespace BDTest;

internal class Runnable
{
    internal Expression<Func<Task>> Task { get; }
    internal Expression<Action> Action { get; }

    internal Runnable(Expression<Func<Task>> task)
    {
        Task = task;
    }

    public Runnable(Expression<Action> action)
    {
        Action = action;
    }

    internal async Task Run()
    {
        if (Task == null && Action == null)
        {
            await ConsoleReporter.WriteLine($"{nameof(Task)} and {nameof(Action)} are both null in {nameof(Runnable)}");
            return;
        }

        if (Task != null)
        {
            await Task.Compile().Invoke();
            return;
        }

        // An Action expression can discard a method's result. Inspect the original
        // body before invoking it: the compiled delegate's return type is always void.
        var resultType = Action.Body.Type;
        if (resultType != typeof(void) && resultType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(method => method.Name == "GetAwaiter" && !method.IsGenericMethod
                    && method.GetParameters().Length == 0))
        {
            throw new InvalidOperationException(
                $"The step returns awaitable type '{resultType.FullName}', but is bound to Action and would discard its result. " +
                "Return Task from the step expression instead. For ValueTask or ValueTask<T>, call AsTask(); " +
                "for other awaitables, use an async Task helper that awaits the operation.");
        }

        Action.Compile().Invoke();
    }

    internal string GetExpressionString()
    {
        return Task?.Body?.ToString() ?? Action?.Body?.ToString();
    }
}
