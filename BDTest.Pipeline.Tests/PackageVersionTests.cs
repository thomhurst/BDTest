using BDTest.Pipeline.Modules;

namespace BDTest.Pipeline.Tests;

public class PackageVersionTests
{
    [Test]
    [Arguments("2.3.60", "2.3.60.0", "2.3.60.0")]
    [Arguments("2.4.0-beta.1", "2.4.0.0", "2.4.0.0")]
    [Arguments("3.0.0", "3.0.0.0", "3.0.0.0")]
    [Arguments("3.1.2-beta.4", "3.0.0.0", "3.1.2.0")]
    [Arguments("3.1.3", "3.0.0.0", "3.1.3.0")]
    [Arguments("3.2.0+build.5", "3.0.0.0", "3.2.0.0")]
    [Arguments("4.2.3-beta.1+build.5", "4.0.0.0", "4.2.3.0")]
    public async Task Pack_passes_explicit_assembly_and_release_versions(
        string packageVersion, string assemblyVersion, string fileVersion)
    {
        var options = PackProjectsModule.CreatePackOptions("library.csproj", "packages", packageVersion);
        var properties = options.Properties!.ToDictionary(property => property.Key, property => property.Value);

        using (Assert.Multiple())
        {
            await Assert.That(options.ProjectSolution).IsEqualTo("library.csproj");
            await Assert.That(options.Configuration).IsEqualTo("Release");
            await Assert.That(options.Output).IsEqualTo("packages");
            await Assert.That(properties["PackageVersion"]).IsEqualTo(packageVersion);
            await Assert.That(properties["Version"]).IsEqualTo(packageVersion);
            await Assert.That(properties["AssemblyVersion"]).IsEqualTo(assemblyVersion);
            await Assert.That(properties["FileVersion"]).IsEqualTo(fileVersion);
            await Assert.That(properties["InformationalVersion"]).IsEqualTo(packageVersion);
        }
    }

    [Test]
    [Arguments("3.0.0", "3.0.1")]
    [Arguments("3.0.1", "3.1.0")]
    [Arguments("4.1.2-beta.1", "4.1.2")]
    public async Task Minor_and_patch_releases_keep_the_same_assembly_identity(string first, string second)
    {
        var firstVersion = PackProjectsModule.CreatePackOptions("library.csproj", "packages", first)
            .Properties!.Single(property => property.Key == "AssemblyVersion").Value;
        var secondVersion = PackProjectsModule.CreatePackOptions("library.csproj", "packages", second)
            .Properties!.Single(property => property.Key == "AssemblyVersion").Value;

        await Assert.That(secondVersion).IsEqualTo(firstVersion);
    }
}
