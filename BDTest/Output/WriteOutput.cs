using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BDTest.Output
{
    public static class WriteOutput
    {
        public const string ResultDirectoryArgumentName = "-ResultsDirectory=";

        public static void OutputData()
        {
            RunReportDll();
        }

        private static void RunReportDll()
        {
            var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var reportDll = Directory.CreateDirectory(outputDirectory).GetFiles("BDTest.ReportGenerator.dll").FirstOrDefault()?.FullName;

            if (outputDirectory == null)
            {
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{reportDll}\" \"{ResultDirectoryArgumentName}{outputDirectory}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }

            };

            process.Start();
        }
    }
}