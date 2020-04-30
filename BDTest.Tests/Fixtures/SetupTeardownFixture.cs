using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures
{
    [SetUpFixture]
    public class SetupTeardownFixture
    {
        [OneTimeTearDown]
        public void DeleteOutputDirectories()
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