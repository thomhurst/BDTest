global using static BDTest.Tests.Helpers.TestValue;

using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;

namespace BDTest.Tests.Helpers;

internal static class TestValue
{
    // Fail at the missing value rather than at a later dereference in the assertion.
    public static T Required<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string expression = "")
        where T : class => value ?? throw new AssertionException($"Expected {expression} to be non-null.");
}
