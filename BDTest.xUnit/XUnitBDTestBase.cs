using BDTest.Settings;
using BDTest.Test;
using Xunit;
using Xunit.Sdk;

namespace BDTest.xUnit;

public abstract class XUnitBDTestBase<TContext> : AbstractContextBDTestBase<TContext>, IDisposable
    where TContext : class, new()
{
    protected XUnitBDTestBase()
    {
        XUnitExceptionRegistration.EnsureRegistered();
        BDTestExecutionId = TestContext.Current.Test?.UniqueID
            ?? throw new InvalidOperationException("An xUnit test must be running to create a BDTest adapter.");
        MarkTestAsStarted();
    }

    protected override string BDTestExecutionId { get; }

    protected override void OnScenarioSkipped(string reason) => Assert.Skip(reason);

    public void Dispose() => MarkTestAsComplete();
}

// Non-generic so all context types share the same CLR-protected initialization.
internal static class XUnitExceptionRegistration
{
    static XUnitExceptionRegistration()
    {
        if (!BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(typeof(SkipException)))
        {
            BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(SkipException));
        }
    }

    internal static void EnsureRegistered() { }
}
