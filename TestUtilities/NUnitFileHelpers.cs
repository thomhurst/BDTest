using NUnit.Framework;

namespace BDTest.Tests.Helpers;

public static class FileHelpers
{
    internal static string OutputRoot { get; } = Path.Combine(Path.GetTempPath(), "BDTest-" + Guid.NewGuid().ToString("N"));

    public static string GetUniqueTestJsonFilePath()
    {
        var outputFolder = GetUniqueTestOutputFolder();

        return Directory.EnumerateFiles(outputFolder, "*.json").Single();
    }

    public static string GetUniqueTestOutputFolder()
    {
        return GetOutputFolder($"test-{TestContext.CurrentContext.Test.ID}-{TestContext.CurrentContext.CurrentRepeatCount}");
    }
        
    public static string GetOutputFolder(string folderName)
    {
        var outputFolder = Path.Combine(OutputRoot, folderName);
            
        Directory.CreateDirectory(outputFolder);

        return outputFolder;
    }
        
    internal static void DeleteOutputRoot()
    {
        if (Directory.Exists(OutputRoot))
        {
            Directory.Delete(OutputRoot, recursive: true);
        }
    }
}
