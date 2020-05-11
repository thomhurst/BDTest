using System.IO;
using System.Runtime.CompilerServices;
using BDTest.Paths;

namespace BDTest
{
    internal static class Initialiser
    {
        private static bool _alreadyRun;
        private static string ExceptionFilePath => Path.Combine(FileLocations.ReportsOutputDirectory, "BDTest - Exception.txt");
        private static string RunExceptionFilePath => Path.Combine(FileLocations.ReportsOutputDirectory, "BDTest - Run Exception.txt");
        private static string ReportExceptionFilePath => Path.Combine(FileLocations.ReportsOutputDirectory, "BDTest - Report Exception.txt");

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Initialise()
        {
            if (_alreadyRun)
            {
                return;
            }

            _alreadyRun = true;

            DeletePreviousData();
        }

        private static void DeletePreviousData()
        {
            if (Directory.Exists(FileLocations.ReportsOutputDirectory) 
                && FileLocations.ReportsOutputDirectory != FileLocations.RawOutputDirectory)
            {
                var files = Directory.GetFiles(FileLocations.ReportsOutputDirectory);

                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }

            if (File.Exists(ExceptionFilePath))
            {
                File.Delete(ExceptionFilePath);
            }
            
            if (File.Exists(RunExceptionFilePath))
            {
                File.Delete(RunExceptionFilePath);
            }
            
            if (File.Exists(ReportExceptionFilePath))
            {
                File.Delete(ReportExceptionFilePath);
            }
        }
    }
}