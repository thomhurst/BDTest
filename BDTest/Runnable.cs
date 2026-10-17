using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
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
                    && method.GetParameters().Length == 0 && IsAwaiterType(method.ReturnType)))
        {
            throw new InvalidOperationException(
                $"The step returns awaitable type '{resultType.FullName}', but is bound to Action and would discard its result. " +
                "Return Task from the step expression instead. For ValueTask or ValueTask<T>, call AsTask(); " +
                "for other awaitables, use an async Task helper that awaits the operation.");
        }

        Action.Compile().Invoke();
    }

    private static bool IsAwaiterType(Type type)
    {
        if (!typeof(INotifyCompletion).IsAssignableFrom(type))
        {
            return false;
        }

        // Reflection on an interface does not include members from its base interfaces.
        var memberTypes = type.IsInterface ? new[] { type }.Concat(type.GetInterfaces()) : new[] { type };
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        return memberTypes.SelectMany(memberType => memberType.GetProperties(flags))
                   .Any(property => property.Name == "IsCompleted" && property.PropertyType == typeof(bool)
                       && property.GetIndexParameters().Length == 0 && property.GetGetMethod() != null)
               && memberTypes.SelectMany(memberType => memberType.GetMethods(flags))
                   .Any(method => method.Name == "GetResult" && !method.IsGenericMethod
                       && method.GetParameters().Length == 0);
    }

    internal string GetExpressionString()
    {
        return Task?.Body?.ToString() ?? Action?.Body?.ToString();
    }
}
