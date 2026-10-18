using System.Reflection;
using BDTest.Attributes;

namespace BDTest.Helpers;

internal static class TagHelper
{
    internal static string[] GetTags(Type testClass, MethodInfo testMethod) =>
        (testClass?.GetCustomAttributes<BDTestTagAttribute>(true) ?? Enumerable.Empty<BDTestTagAttribute>())
        .Concat(testMethod?.GetCustomAttributes<BDTestTagAttribute>(true) ?? Enumerable.Empty<BDTestTagAttribute>())
        .Select(attribute => attribute.Tag)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
