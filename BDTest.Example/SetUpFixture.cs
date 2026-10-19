using NUnit.Framework;
using BDTest;

namespace BDTest.Example;

[SetUpFixture]
public class SetUpFixture
{
    [OneTimeTearDown]
    public void WriteTestData()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "BDTest-Example-" + Guid.NewGuid().ToString("N"));
        try
        {
            HtmlReportGenerationExample.GenerateReports(outputDirectory);
        }
        finally
        {
            NUnitBDTestAssembly.Dispose(typeof(SetUpFixture).Assembly);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
