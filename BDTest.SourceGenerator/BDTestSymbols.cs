using Microsoft.CodeAnalysis;

namespace BDTest.Compiler;

// Shared by the metadata generator and analyzers; always inspect the bound method.
internal static class BDTestSymbols
{
    internal static bool IsStepName(string? name) => name is "Given" or "When" or "Then" or "And" or "But" or "Step";

    internal static bool IsBuilderMethod(IMethodSymbol method)
    {
        // The root namespace also contains unrelated consumer APIs. Identify the
        // host/builder hierarchy instead of accepting every method in that namespace.
        for (var type = method.ContainingType; type != null; type = type.BaseType)
        {
            if (type.ContainingNamespace.ToDisplayString() == "BDTest" &&
                type.ContainingAssembly.Name == "BDTest" &&
                type.Name is "BDTestBase" or "StepBuilder")
                return true;
        }
        return false;
    }
}
