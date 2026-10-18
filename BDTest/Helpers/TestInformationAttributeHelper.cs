using System.Diagnostics;
using System.Reflection;
using BDTest.Attributes;

namespace BDTest.Helpers;

public static class TestInformationAttributeHelper
{
    public static TestInformationAttribute[] GetTestInformationAttributes() => GetTestInformationAttributes(null);

    internal static TestInformationAttribute[] GetTestInformationAttributes(MethodInfo testMethod) =>
        GetTestInformationAttributes(testMethod, StackFramesHelper.GetStackFrames());

    internal static TestInformationAttribute[] GetTestInformationAttributes(MethodInfo testMethod,
        IEnumerable<StackFrame> stackFrames)
    {
        var method = StackFramesHelper.GetMethods(stackFrames).FirstOrDefault(it =>
        {
            var testInformationAttribute = GetBDTestInformationAttributes(it);
            return testInformationAttribute.Any();
        });

        if (method != null)
        {
            return GetBDTestInformationAttributes(method).ToArray();
        }

        return testMethod?.GetCustomAttributes<TestInformationAttribute>(true).ToArray()
            ?? Array.Empty<TestInformationAttribute>();
    }
        
    private static IEnumerable<TestInformationAttribute> GetBDTestInformationAttributes(ICustomAttributeProvider it)
    {
        return it.GetCustomAttributes(typeof(TestInformationAttribute), true) as TestInformationAttribute[] ?? Enumerable.Empty<TestInformationAttribute>();
    }
}