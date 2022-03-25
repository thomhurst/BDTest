using System.Diagnostics;
using System.Reflection;
using BDTest.Attributes;

namespace BDTest.Helpers;

public static class TestInformationAttributeHelper
{
    public static TestInformationAttribute[] GetTestInformationAttributes()
    {
        var stackFrame = StackFramesHelper.GetStackFrames().FirstOrDefault(it =>
        {
            var testInformationAttribute = GetTestInformationAttribute(it);
            return testInformationAttribute != null && testInformationAttribute.Any();
        });

        if (stackFrame != null)
        {
            return GetTestInformationAttribute(stackFrame)?.ToArray() ?? Array.Empty<TestInformationAttribute>();
        }

        return Array.Empty<TestInformationAttribute>();
    }
        
    private static IEnumerable<TestInformationAttribute> GetTestInformationAttribute(StackFrame it)
    {
        return GetBDTestInformationAttributes(it.GetMethod());
    }

    private static IEnumerable<TestInformationAttribute> GetBDTestInformationAttributes(ICustomAttributeProvider it)
    {
        return it.GetCustomAttributes(typeof(TestInformationAttribute), true) as TestInformationAttribute[] ?? Enumerable.Empty<TestInformationAttribute>();
    }
}