using System.Reflection;
using Xunit.v3;

namespace BDTest;

// Existing fixtures may reimplement IAsyncLifetime and bypass the base hook.
// Bind their buffered constructor/setup output before invoking the test body.
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
internal sealed class BDTestLifecycleAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest, IXunitTest test) =>
        XUnitBDTestBase<object>.BeginPendingExecution();
}
