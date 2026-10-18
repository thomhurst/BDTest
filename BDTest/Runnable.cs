using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BDTest;

internal class Runnable
{
    internal Expression<Func<Task>> Task { get; }
    internal Expression<Action> Action { get; }
    private readonly Expression<Func<CancellationToken, Task>> _cancellableTask;
    // Keep compilation lazy so skipped definitions are never compiled. Lazy also
    // shares one delegate safely when the same definition executes concurrently.
    private readonly Lazy<Action> _compiledAction;
    private readonly Lazy<Func<Task>> _compiledTask;
    private readonly Lazy<Func<CancellationToken, Task>> _compiledCancellableTask;
    internal bool AcceptsCancellationToken => _cancellableTask != null;
    internal Expression ExpressionBody => Action?.Body ?? Task?.Body ?? _cancellableTask?.Body;

    internal Runnable(Expression<Func<Task>> step)
    {
        Task = step ?? throw new ArgumentNullException(nameof(step));
        _compiledTask = new Lazy<Func<Task>>(step.Compile);
    }

    internal Runnable(Expression<Func<CancellationToken, Task>> step)
    {
        _cancellableTask = step ?? throw new ArgumentNullException(nameof(step));
        _compiledCancellableTask = new Lazy<Func<CancellationToken, Task>>(step.Compile);
    }

    public Runnable(Expression<Action> step)
    {
        Action = step ?? throw new ArgumentNullException(nameof(step));
        _compiledAction = new Lazy<Action>(step.Compile);
    }

    internal async Task Run(CancellationToken cancellationToken = default)
    {
        if (_cancellableTask != null)
        {
            await _compiledCancellableTask.Value.Invoke(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (Task != null)
        {
            await _compiledTask.Value.Invoke();
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

        _compiledAction.Value.Invoke();
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
        var completionMembers = GetCompletionMembers(type).ToArray();
        return completionMembers.Length == 1 && completionMembers.OfType<PropertyInfo>()
                   .Any(property => property.Name == "IsCompleted" && property.PropertyType == typeof(bool)
                       && property.GetIndexParameters().Length == 0 && property.GetGetMethod() != null && !property.GetGetMethod().IsStatic)
               && memberTypes.SelectMany(memberType => memberType.GetMethods(flags))
                   .Any(method => method.Name == "GetResult" && !method.IsGenericMethod
                       && method.GetParameters().Length == 0);
    }

    private static IEnumerable<MemberInfo> GetCompletionMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;
        if (type.IsInterface)
        {
            var declarations = new[] { type }.Concat(type.GetInterfaces())
                .Select(memberType => new { Type = memberType, Members = memberType.GetMember("IsCompleted", flags) })
                .Where(declaration => declaration.Members.Length != 0).ToArray();
            return declarations.Where(declaration => !declarations.Any(other =>
                    other.Type != declaration.Type && declaration.Type.IsAssignableFrom(other.Type)))
                .SelectMany(declaration => declaration.Members);
        }

        // Look up the name before validating its shape: a derived member hides a
        // base property even when the derived member is not a Boolean property.
        for (var current = type; current != null; current = current.BaseType)
        {
            var members = current.GetMember("IsCompleted", flags);
            if (members.Length != 0)
            {
                return members;
            }
        }

        return Array.Empty<MemberInfo>();
    }

    internal string GetExpressionString()
    {
        return ExpressionBody?.ToString();
    }
}
