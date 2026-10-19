using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class TestIsolationTests
{
    [Test, Repeat(3)]
    public void ExampleReportsUseOwnedOutputAndCleanupPreservesOtherDirectories()
    {
        var foreignFolder = Path.Combine(Path.GetTempPath(), "BDTest-foreign-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(foreignFolder);
        var stale = Path.Combine(foreignFolder, "stale.json");
        try
        {
            File.WriteAllText(stale, "stale output from another run");
            var folder = FileHelpers.GetUniqueTestOutputFolder();
            Assert.That(folder, Does.StartWith(FileHelpers.OutputRoot + Path.DirectorySeparatorChar));
            Assert.That(Directory.GetFiles(folder), Is.Empty);
            Example.HtmlReportGenerationExample.GenerateReports(folder);
            var json = FileHelpers.GetUniqueTestJsonFilePath();
            Assert.That(File.ReadAllText(json), Does.Contain("schemaVersion").And.Contain("scenarios"));
            Assert.That(Directory.GetFiles(folder, "*.html"), Has.Length.EqualTo(1));

            FileHelpers.DeleteOutputRoot();
            Assert.That(Directory.Exists(FileHelpers.OutputRoot), Is.False);
            Assert.That(File.ReadAllText(stale), Is.EqualTo("stale output from another run"));
        }
        finally
        {
            FileHelpers.DeleteOutputRoot();
            Directory.Delete(foreignFolder, recursive: true);
        }
    }

}
