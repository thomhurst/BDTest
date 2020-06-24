using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BDTest.Settings;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [SetUpFixture]
    public class SetupTeardownFixture
    {
        [OneTimeSetUp]
        public void Settings()
        {
            BDTestSettings.Environment = "Local";
            BDTestSettings.Tag = "AcceptanceTests";
        }
        
        [OneTimeTearDown]
        public async Task DeleteOutputDirectories()
        {
            var bdTestOutputs = Directory
                .GetDirectories(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .Where(directory => Path.GetFileName(directory).StartsWith("BDTest-"));
            
            foreach (var bdTestOutput in bdTestOutputs)
            {
                Directory.Delete(bdTestOutput, true);
            }
        }
    }
}