using System;
using System.Reflection;
using BDTest.Test;

namespace BDTest.Extensions
{
    internal static class BuildableTestExtensions
    {
        internal static MethodInfo GetTestMethod(this BuildableTest buildableTest) => buildableTest?.BdTestBase?.GetType()?.GetMethod(buildableTest.CallerMember);
        internal static Type GetTestClass(this BuildableTest buildableTest) => buildableTest?.BdTestBase?.GetType();
    }
}