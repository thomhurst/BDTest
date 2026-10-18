using System;
using Microsoft.CodeAnalysis;

namespace BDTest.Compiler;

// Shared by the metadata generator and analyzers; always inspect the bound method.
internal static class BDTestSymbols
{
    internal static bool IsStepName(string? name) => name is "Given" or "When" or "Then" or "And" or "But";

    internal static bool IsBuilderMethod(IMethodSymbol method)
    {
        var type = method.ContainingType;
        var space = type.ContainingNamespace.ToDisplayString();
        return (type.Name == "BDTestBase" && space == "BDTest.Test") ||
               space == "BDTest.Test.Steps" || space.StartsWith("BDTest.Test.Steps.", StringComparison.Ordinal);
    }
}
