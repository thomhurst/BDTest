using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [SetUpFixture]
    public class SetupTeardownFixture
    {
        [OneTimeTearDown]
        public async Task DeleteOutputDirectories()
        {
            // var bdTestOutputs = Directory
            //     .GetDirectories(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
            //     .Where(directory => Path.GetFileName(directory).StartsWith("BDTest-"));
            //
            // foreach (var bdTestOutput in bdTestOutputs)
            // {
            //     Directory.Delete(bdTestOutput, true);
            // }
            
            var uri = new Uri("https://bdtest-reportserver.azurewebsites.net");
            
            uri = new UriBuilder
            {
                Scheme = "https",
                Host = "localhost",
                Port = 44329
            }.Uri;
            
            var url = await BDTestReportServer.SendDataAndGetReportUri(uri);
            var absoluteUrl = url.AbsoluteUri;
            Console.WriteLine(absoluteUrl);
            Process.Start("explorer", absoluteUrl);
        }
    }
}