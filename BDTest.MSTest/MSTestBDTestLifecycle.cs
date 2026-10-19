using System.Reflection;
using System.Threading;
using BDTest.Settings;
using BDTest.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest;

/// <summary>Connects MSTest's native context injection to the shared BDTest lifecycle.</summary>
public static class MSTestBDTestLifecycle
{
    internal static readonly AsyncLocal<Invocation?> Current = new();

    /// <summary>The outcome declarations to register before starting an explicit run.</summary>
    public static FrameworkOutcomeMap OutcomeMap => MSTestExceptionRegistration.OutcomeMap;

    /// <summary>Starts the fixture's host synchronously when MSTest injects TestContext, before setup.</summary>
    /// <remarks>The BDTestClass wrapper awaits completion after MSTest's cleanup, including failed tests.</remarks>
    public static void Initialize(IBDTestMSTestTest test, TestContext context)
    {
        if (test == null) throw new ArgumentNullException(nameof(test));
        if (context == null) throw new ArgumentNullException(nameof(context));
        var invocation = Current.Value ?? throw new InvalidOperationException(
            "Replace [TestClass] with [BDTestClass] to supply MSTest's test MethodInfo and lifecycle.");
        invocation.Initialize(test.BDTestHost, context);
    }

    internal sealed class Invocation(MethodInfo method)
    {
        private BDTestBase? _host;
        private TestContext? _context;

        internal CancellationToken CancellationToken => _context?.CancellationToken ?? default;

        internal void Initialize(BDTestBase host, TestContext context)
        {
            if (host == null) throw new InvalidOperationException("BDTestHost must return a stable host.");
            if (_host != null)
            {
                if (!ReferenceEquals(_host, host))
                    throw new InvalidOperationException("BDTestHost changed during an MSTest invocation.");
                return;
            }
            // Display names include data rows; the suffix distinguishes repeats with identical data.
            host.BeginTest($"{context.FullyQualifiedTestClassName}.{context.TestDisplayName ?? context.TestName}_{Guid.NewGuid():N}", method);
            _context = context;
            _host = host;
        }

        internal ValueTask CompleteAsync() => _host?.EndTestAsync() ?? default;
    }
}
