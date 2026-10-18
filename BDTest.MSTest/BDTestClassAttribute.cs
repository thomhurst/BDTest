using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BDTest.MSTest;

/// <summary>Supplies MSTest's exact method identity to the BDTest lifecycle.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BDTestClassAttribute : TestClassAttribute
{
    public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute) =>
        new MethodIdentityAttribute(testMethodAttribute)
        {
            DisplayName = testMethodAttribute.DisplayName,
            UnfoldingStrategy = testMethodAttribute.UnfoldingStrategy
        };

#pragma warning disable MSTEST0057 // This internal wrapper preserves the original attribute's source location.
    private sealed class MethodIdentityAttribute(TestMethodAttribute inner)
        : TestMethodAttribute(inner.DeclaringFilePath, inner.DeclaringLineNumber ?? -1)
    {
        public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            var previous = MSTestMethodIdentity.Current.Value;
            MSTestMethodIdentity.Current.Value = testMethod.MethodInfo;
            try
            {
                return await inner.ExecuteAsync(testMethod);
            }
            finally
            {
                MSTestMethodIdentity.Current.Value = previous;
            }
        }
    }
#pragma warning restore MSTEST0057
}

internal static class MSTestMethodIdentity
{
    internal static readonly AsyncLocal<MethodInfo?> Current = new();
}
