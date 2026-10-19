global using static BDTest.Tests.Helpers.TestValue;

using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace BDTest.Tests.Helpers;

internal static class TestValue
{
    // For synchronous callbacks and allocation/lifetime probes: introducing an async
    // state machine would change the behavior under test.
    public static void RequireCondition(bool condition, string message)
    {
        if (!condition) throw new AssertionException(message);
    }

    // Fail at the missing value rather than at a later dereference in the assertion.
    public static T Required<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string expression = "")
        where T : class => value ?? throw new AssertionException($"Expected {expression} to be non-null.");
}
