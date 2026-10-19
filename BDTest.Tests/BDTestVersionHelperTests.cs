using System.Reflection;
using BDTest.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests;

public class BDTestVersionHelperTests
{
    [Test]
    public async Task Report_version_uses_the_full_release_version()
    {
        var version = typeof(BDTestVersionHelper).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        await Assert.That(BDTestVersionHelper.CurrentVersion).IsEqualTo(version);
    }
}
