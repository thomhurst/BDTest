using BDTest.Helpers;
using NUnit.Framework;

namespace BDTest.Example;

[SetUpFixture]
public class SetUpFixture
{
    [OneTimeTearDown]
    public void WriteTestData()
    {
        // Use the example class to generate reports
        HtmlReportGenerationExample.GenerateReports();
    }
}