using BDTest.Pipeline.Modules;
using NUnit.Framework;

namespace BDTest.Pipeline.Tests;

public class PackageVersionTests
{
    [TestCase("2.3.60", "2.3.60.0", "2.3.60.0")]
    [TestCase("2.4.0-beta.1", "2.4.0.0", "2.4.0.0")]
    [TestCase("3.0.0", "3.0.0.0", "3.0.0.0")]
    [TestCase("3.1.2-beta.4", "3.0.0.0", "3.1.2.0")]
    [TestCase("3.1.3", "3.0.0.0", "3.1.3.0")]
    [TestCase("3.2.0+build.5", "3.0.0.0", "3.2.0.0")]
    [TestCase("4.2.3-beta.1+build.5", "4.0.0.0", "4.2.3.0")]
    public void Pack_passes_explicit_assembly_and_release_versions(
        string packageVersion, string assemblyVersion, string fileVersion)
    {
        var options = PackProjectsModule.CreatePackOptions("library.csproj", "packages", packageVersion);
        var properties = options.Properties!.ToDictionary(property => property.Key, property => property.Value);

        Assert.Multiple(() =>
        {
            Assert.That(options.ProjectSolution, Is.EqualTo("library.csproj"));
            Assert.That(options.Configuration, Is.EqualTo("Release"));
            Assert.That(options.Output, Is.EqualTo("packages"));
            Assert.That(properties["PackageVersion"], Is.EqualTo(packageVersion));
            Assert.That(properties["Version"], Is.EqualTo(packageVersion));
            Assert.That(properties["AssemblyVersion"], Is.EqualTo(assemblyVersion));
            Assert.That(properties["FileVersion"], Is.EqualTo(fileVersion));
            Assert.That(properties["InformationalVersion"], Is.EqualTo(packageVersion));
        });
    }

    [TestCase("3.0.0", "3.0.1")]
    [TestCase("3.0.1", "3.1.0")]
    [TestCase("4.1.2-beta.1", "4.1.2")]
    public void Minor_and_patch_releases_keep_the_same_assembly_identity(string first, string second)
    {
        var firstVersion = PackProjectsModule.CreatePackOptions("library.csproj", "packages", first)
            .Properties!.Single(property => property.Key == "AssemblyVersion").Value;
        var secondVersion = PackProjectsModule.CreatePackOptions("library.csproj", "packages", second)
            .Properties!.Single(property => property.Key == "AssemblyVersion").Value;

        Assert.That(secondVersion, Is.EqualTo(firstVersion));
    }
}
