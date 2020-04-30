using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace BDTest.Tests.Helpers
{
    public static class FileHelpers
    {
        public static string GetUniqueTestJsonFilePath()
        {
            var outputFolder = GetUniqueTestOutputFolder();

            var files = Directory.GetFiles(outputFolder);

            return files.FirstOrDefault(file => Path.GetExtension(file) == ".json");
        }

        public static string GetUniqueTestOutputFolder()
        {
            return GetOutputFolder(
                "BDTest-"
                + TestContext.CurrentContext.Test.FullName
                + TestContext.CurrentContext.Test.ID
            );
        }
        
        public static string GetOutputFolder(string folderName)
        {
            var outputFolder = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                folderName
            );
            
            Directory.CreateDirectory(outputFolder);

            return outputFolder;
        }
    }
}