using BDTest.Settings;
using BDTest.Test;
using TUnit.Core;
using TUnit.Core.Extensions;

namespace BDTest;

/// <summary>Framework hooks shared by composed tests and the optional base class.</summary>
public static class TUnitBDTestLifecycle
{
    private const string HostKey = "BDTest.TUnit.Host";

    /// <summary>The outcome declarations to register before starting an explicit run.</summary>
    public static FrameworkOutcomeMap OutcomeMap => TUnitExceptionRegistration.OutcomeMap;

    [BeforeEvery(HookType.Test, Order = int.MinValue)]
    public static void BeforeTest(TestContext context)
    {
        if (context.Metadata.TestDetails.ClassInstance is not IBDTestTUnitTest test) return;
        var host = test.BDTestHost ?? throw new InvalidOperationException("BDTestHost must return a stable host.");
        Begin(host, context);
        context.StateBag[HostKey] = host;
    }

    [AfterEvery(HookType.Test, Order = int.MaxValue - 1)]
    public static Task AfterTest(TestContext context)
    {
        if (!context.StateBag.TryRemove(HostKey, out var value) || value is not BDTestBase host)
            return Task.CompletedTask;
        // Complete in the caller's flow before returning the asynchronous disposal.
        try { return host.EndTestAsync().AsTask(); }
        finally { context.AddAsyncLocalValues(); }
    }

    internal static void Begin(BDTestBase host, TestContext? context)
    {
        host.BeginTest(context?.Id ?? Guid.NewGuid().ToString("N"),
            context?.Metadata.TestDetails.MethodMetadata.GetReflectionInfo());
        context?.AddAsyncLocalValues();
    }
}
