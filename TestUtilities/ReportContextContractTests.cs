using BDTest.Helpers;
#if BDTEST_NUNIT
using TestAttribute = NUnit.Framework.TestAttribute;
#elif BDTEST_MSTEST
using TestAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#elif BDTEST_TUNIT
using TestAttribute = TUnit.Core.TestAttribute;
#elif BDTEST_XUNIT
using TestAttribute = Xunit.FactAttribute;
#endif

namespace BDTest.Adapters.Conformance;

#if BDTEST_MSTEST
[Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
public class ReportContextContractTests
{
#if BDTEST_MSTEST
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = null!;
#endif

    [Test]
    public void Native_context_can_supply_report_metadata()
    {
#if BDTEST_NUNIT
        var context = NUnit.Framework.TestContext.CurrentContext;
        var descriptor = context.ToBDTestRunDescriptor(value => new BDTestRunDescriptor { Tag = value.Test.ID });
#elif BDTEST_MSTEST
        var context = TestContext;
        var descriptor = context.ToBDTestRunDescriptor(value => new BDTestRunDescriptor { Tag = value.TestName });
#elif BDTEST_TUNIT
        var context = global::TUnit.Core.TestContext.Current!;
        var descriptor = context.ToBDTestRunDescriptor(value => new BDTestRunDescriptor { Tag = value.Id });
#elif BDTEST_XUNIT
        var context = Xunit.TestContext.Current;
        var descriptor = context.ToBDTestRunDescriptor(value => new BDTestRunDescriptor { Tag = value.Test!.UniqueID });
#endif
        if (string.IsNullOrEmpty(descriptor.Tag)) throw new InvalidOperationException("Missing native context identity.");
        descriptor.CustomProperties["Native <context>"] = "Selected & mapped";
        using var run = BDTestRun.Start();
        var calls = 0;
        var html = context.CreateHtmlReport(run, value =>
        {
            if (!ReferenceEquals(context, value)) throw new InvalidOperationException("Context was substituted.");
            calls++;
            return descriptor;
        });
        if (calls != 1 || !html.Contains("Native &lt;context&gt;") || !html.Contains("Selected &amp; mapped"))
            throw new InvalidOperationException("Context metadata was not safely rendered exactly once.");
        var report = BDTestJsonHelper.DeserializeReport(BDTestJsonHelper.GetTestJsonData(descriptor, run));
        if (report.Tag != descriptor.Tag || report.CustomProperties!["Native <context>"] != "Selected & mapped")
            throw new InvalidOperationException("Context metadata was not preserved in JSON.");
    }
}
