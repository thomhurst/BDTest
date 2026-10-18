using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

/// <summary>Owns BDTest completion around MSTest's native setup, test and cleanup.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BDTestClassAttribute : TestClassAttribute
{
    public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute) =>
        new LifecycleAttribute(testMethodAttribute)
        {
            DisplayName = testMethodAttribute.DisplayName,
            UnfoldingStrategy = testMethodAttribute.UnfoldingStrategy
        };

#pragma warning disable MSTEST0057 // This internal wrapper preserves the original attribute's source location.
    private sealed class LifecycleAttribute(TestMethodAttribute inner)
        : TestMethodAttribute(inner.DeclaringFilePath, inner.DeclaringLineNumber ?? -1)
    {
        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) =>
            inner.ExecuteAsync(new LifecycleTestMethod(testMethod));
    }
#pragma warning restore MSTEST0057

    // Keep the original attribute in control, including custom repeats. Each native
    // invocation creates its own fixture, context injection and completion boundary.
    private sealed class LifecycleTestMethod(ITestMethod inner) : ITestMethod
    {
        public string TestMethodName => inner.TestMethodName;
        public string TestClassName => inner.TestClassName;
        public Type ReturnType => inner.ReturnType;
        public object[] Arguments => inner.Arguments;
        public ParameterInfo[] ParameterTypes => inner.ParameterTypes;
        public MethodInfo MethodInfo => inner.MethodInfo;
        public Attribute[] GetAllAttributes() => inner.GetAllAttributes();
        public TAttributeType[] GetAttributes<TAttributeType>() where TAttributeType : Attribute =>
            inner.GetAttributes<TAttributeType>();

        public async Task<TestResult> InvokeAsync(object[] arguments)
        {
            var previous = MSTestBDTestLifecycle.Current.Value;
            var invocation = new MSTestBDTestLifecycle.Invocation(MethodInfo);
            MSTestBDTestLifecycle.Current.Value = invocation;
            TestResult? result = null;
            try
            {
                result = await inner.InvokeAsync(arguments);
            }
            finally
            {
                try { await invocation.CompleteAsync(); }
                catch (Exception exception) when (result != null)
                {
                    result.Outcome = UnitTestOutcome.Failed;
                    result.TestFailureException = result.TestFailureException == null
                        ? exception : new AggregateException(result.TestFailureException, exception);
                }
                finally { MSTestBDTestLifecycle.Current.Value = previous; }
            }
            return result;
        }
    }
}
