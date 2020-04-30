using System.IO;
using System.Linq;
using System.Reflection;

namespace BDTest.Tests
{
    public static class FileHelpers
    {
        public static string GetJsonFilePath()
        {
            var outputFolder = GetOutputFolder();

            var files = Directory.GetFiles(outputFolder);

            return files.FirstOrDefault(file => Path.GetExtension(file) == ".json");
        }

        public static bool HasCustomFolder()
        {
            return Path.GetFileName(GetOutputFolder()) == BDTestSettings.ReportFolderName;
        }

        public static string GetOutputFolder()
        {
            var outputFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (!string.IsNullOrEmpty(BDTestSettings.ReportFolderName))
            {
                outputFolder = Path.Combine(outputFolder, BDTestSettings.ReportFolderName);
            }

            Directory.CreateDirectory(outputFolder);

            return outputFolder;
        }
    }
}