using System.IO;

namespace BDTest.ReportGenerator.Utils
{
    public static class CopyFolder
    {
        public static void Copy(string sourcePath, string destinationPath)
        {
            //Now Create all of the directories
            foreach (var dirPath in Directory.GetDirectories(sourcePath, "*",
                SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));

            //Copy all the files & Replaces any files with the same name
            foreach (var newPath in Directory.GetFiles(sourcePath, "*.*",
                SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, destinationPath), true);
        }
    }
}
