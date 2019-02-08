using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BDTest.Paths;

namespace BDTest
{
    internal static class Initialiser
    {
        private static bool _alreadyRun;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Initialise()
        {
            if (_alreadyRun)
            {
                return;
            }

            _alreadyRun = true;

            BDTestSettings.Debug = false;

            DeletePreviousData();
        }

        private static void DeletePreviousData()
        {
            if (Directory.Exists(FileLocations.ScenariosDirectory))
            {
                foreach (var filePath in Directory.GetFiles(FileLocations.ScenariosDirectory))
                {
                    File.Delete(filePath);
                }
            }

            Directory.CreateDirectory(FileLocations.ScenariosDirectory);

            if (File.Exists(FileLocations.Warnings))
            {
                File.Delete(FileLocations.Warnings);
            }

            var runtimeConfigFile = Directory.GetFiles(FileLocations.OutputDirectory)
                .FirstOrDefault(it => it.EndsWith(".runtimeconfig.dev.json"));

            if (runtimeConfigFile == null)
            {
                return;
            }

            var bdTestReportRunConfigPath = Path.Combine(FileLocations.OutputDirectory,
                "BDTest.ReportGenerator.runtimeconfig.dev.json");

            if (File.Exists(bdTestReportRunConfigPath))
            {
                File.Delete(bdTestReportRunConfigPath);
            }

            File.Copy(runtimeConfigFile,
                bdTestReportRunConfigPath);

            if (File.Exists(Path.Combine(FileLocations.OutputDirectory, "BDTest - Exception.txt")))
            {
                File.Delete(Path.Combine(FileLocations.OutputDirectory, "BDTest - Exception.txt"));
            }

            if (File.Exists(Path.Combine(FileLocations.OutputDirectory, "BDTest - Run Exception.txt")))
            {
                File.Delete(Path.Combine(FileLocations.OutputDirectory, "BDTest - Run Exception.txt"));
            }

            if (File.Exists(Path.Combine(FileLocations.OutputDirectory, "BDTest - Report Exception.txt")))
            {
                File.Delete(Path.Combine(FileLocations.OutputDirectory, "BDTest - Report Exception.txt"));
            }
        }
    }
}