using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Helpers;
using BDTest.Test;

namespace BDTest.Extensions;

internal static class BuildableTestExtensions
{
    internal static MethodInfo GetTestMethod(this BuildableTest buildableTest)
    {
        var candidates = buildableTest?.BdTestBase?.GetType().GetMethods()
            .Where(method => method.Name == buildableTest.CallerMember).ToArray();
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        foreach (var frame in StackFramesHelper.GetStackFrames())
        {
            var executingMethod = frame.GetMethod();
            foreach (var candidate in candidates)
            {
                if (candidate.Equals(executingMethod))
                {
                    return candidate;
                }

                var stateMachineType = candidate.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
                var executingType = executingMethod?.DeclaringType;
                if (stateMachineType != null && executingMethod.Name == "MoveNext" &&
                    NormalizeGenericType(stateMachineType) == NormalizeGenericType(executingType))
                {
                    return candidate;
                }
            }
        }

        // A name alone cannot identify an overload. Do not borrow another test's metadata.
        return null;
    }

    private static Type NormalizeGenericType(Type type) =>
        type?.IsGenericType == true ? type.GetGenericTypeDefinition() : type;

    internal static Type GetTestClass(this BuildableTest buildableTest) => buildableTest?.BdTestBase?.GetType();
}
