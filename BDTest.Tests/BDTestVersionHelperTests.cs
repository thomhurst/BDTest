using System.Reflection;
using BDTest.Helpers;
using NUnit.Framework;

namespace BDTest.Tests;

public class BDTestVersionHelperTests
{
    [Test]
    public void Report_version_uses_the_full_release_version()
    {
        var version = typeof(BDTestVersionHelper).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.That(BDTestVersionHelper.CurrentVersion, Is.EqualTo(version));
    }
}
