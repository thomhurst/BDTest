using BDTest.Helpers;
using NUnit.Framework;

namespace BDTest.Example;

/// <summary>
/// Example demonstrating how to generate HTML reports using BDTestHtmlHelper.
/// This shows the pattern for generating both JSON and HTML reports after test execution.
/// </summary>
public class HtmlReportGenerationExample
{
    /// <summary>
    /// Example method showing how to generate HTML reports.
    /// This can be called in a SetUpFixture OneTimeTearDown method or after test execution.
    /// </summary>
    public static void GenerateReports()
    {
        var outputDirectory = Directory.GetCurrentDirectory();
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        
        // Generate reports with custom run descriptor
        var runDescriptor = new BDTestRunDescriptor
        {
            Environment = "Production",
            Tag = "Release-v1.0",
            BranchName = "main"
        };
        
        // Generate JSON report (existing functionality)
        var jsonData = BDTestJsonHelper.GetTestJsonData(runDescriptor);
        var jsonPath = Path.Combine(outputDirectory, $"BDTest-Report-{timestamp}.json");
        File.WriteAllText(jsonPath, jsonData);
        Console.WriteLine($"JSON report generated: {jsonPath}");
        
        // Generate HTML report (new functionality)
        var htmlData = BDTestHtmlHelper.GetTestHtmlReport(runDescriptor);
        var htmlPath = Path.Combine(outputDirectory, $"BDTest-Report-{timestamp}.html");
        File.WriteAllText(htmlPath, htmlData);
        Console.WriteLine($"HTML report generated: {htmlPath}");
        Console.WriteLine($"Open {htmlPath} in a web browser to view the report");
        Console.WriteLine("This HTML file can be published to Azure DevOps build summary tabs");
    }

    /// <summary>
    /// Alternative method showing simple report generation without custom descriptors
    /// </summary>
    public static void GenerateSimpleReports()
    {
        var outputDirectory = Directory.GetCurrentDirectory();
        
        // Generate reports with default settings
        var jsonData = BDTestJsonHelper.GetTestJsonData();
        File.WriteAllText(Path.Combine(outputDirectory, "BDTest-Report.json"), jsonData);
        
        var htmlData = BDTestHtmlHelper.GetTestHtmlReport();
        File.WriteAllText(Path.Combine(outputDirectory, "BDTest-Report.html"), htmlData);
    }
}