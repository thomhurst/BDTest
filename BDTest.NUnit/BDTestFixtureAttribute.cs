using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace BDTest;

/// <summary>Wraps each NUnit setup, test body and teardown in one BDTest execution.</summary>
/// <remarks>The fixture must implement INUnitBDTest. Apply to a method too when it declares other setup/teardown wrappers.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class BDTestFixtureAttribute : NUnitAttribute, IWrapSetUpTearDown
{
    public TestCommand Wrap(TestCommand command) => new LifecycleCommand(command);

    private sealed class LifecycleCommand(TestCommand command) : DelegatingTestCommand(command)
    {
        public override TestResult Execute(TestExecutionContext context)
        {
            NUnitTestLifecycle? lifecycle = null;
            try
            {
                var host = (context.TestObject as INUnitBDTest)?.BDTestHost;
                if (host is not INUnitBDTestLifecycle adapter || !ReferenceEquals(adapter.TestInstance, context.TestObject))
                    throw new InvalidOperationException("BDTestFixture requires INUnitBDTest.BDTestHost to return an NUnit host owned by the current fixture.");

                lifecycle = adapter.Lifecycle;
                lifecycle.BeginWrapped(context.CurrentTest.Id, context.CurrentRepeatCount, context.CurrentTest.Method?.MethodInfo);
                context.CurrentResult = innerCommand.Execute(context);
            }
            catch (Exception error)
            {
                context.CurrentResult.RecordException(error);
            }
            finally
            {
                if (lifecycle != null)
                {
                    try
                    {
                        lifecycle.CompleteWrapped();
                    }
                    catch (Exception error)
                    {
                        context.CurrentResult.RecordTearDownException(error);
                    }
                }
            }
            return context.CurrentResult;
        }
    }
}

internal interface INUnitBDTestLifecycle
{
    object TestInstance { get; }
    NUnitTestLifecycle Lifecycle { get; }
}

internal sealed class NUnitTestLifecycle(Action<string, MethodInfo?> begin, Action complete)
{
    internal static void CompleteOnRunner(Func<ValueTask> complete)
    {
        var synchronization = SynchronizationContext.Current;
        try
        {
            // Start synchronous disposal on the runner thread, but prevent async
            // disposal from posting back to the runner while it waits here.
            SynchronizationContext.SetSynchronizationContext(null);
            complete().AsTask().GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(synchronization); }
    }

    private readonly AsyncLocal<bool> _wrapped = new();
    internal bool IsWrapped => _wrapped.Value;

    internal void Begin(string id, int repeat, MethodInfo? method) => begin($"{id}_{repeat}", method);

    internal void BeginWrapped(string id, int repeat, MethodInfo? method)
    {
        _wrapped.Value = true;
        Begin(id, repeat, method);
    }

    internal void CompleteWrapped()
    {
        try { complete(); }
        finally { _wrapped.Value = false; }
    }
}
