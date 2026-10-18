using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using NUnit.Framework;

namespace BDTest.Pipeline.Tests;

public class PackageArtifactVerificationTests
{
    private string _packageDirectory = null!;
    private string _scriptPath = null!;

    [SetUp]
    public void SetUp()
    {
        _packageDirectory = Path.Combine(Path.GetTempPath(), "BDTest artifact verification", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_packageDirectory);
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "scripts", "Test-PackageArtifacts.ps1")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Cannot locate the repository's artifact verification script.");
        }

        _scriptPath = Path.Combine(directory.FullName, "scripts", "Test-PackageArtifacts.ps1");

        foreach (var id in new[] { "BDTest", "BDTest.NUnit", "BDTest.xUnit", "BDTest.MSTest", "BDTest.TUnit", "BDTest.Razor.Reports" })
        {
            CreatePackage(id, ".nupkg");
            CreatePackage(id, ".snupkg");
        }
        CreatePackage("BDTest.SourceGenerator", ".nupkg");
        CreatePackage("BDTest.Analyzers", ".nupkg");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_packageDirectory, recursive: true);

    [Test]
    public async Task Rejects_a_missing_primary_package()
    {
        File.Delete(PackagePath("BDTest", ".nupkg"));

        await AssertRejected("primary packages", "found 7");
    }

    [Test]
    public async Task Rejects_a_missing_symbol_package()
    {
        File.Delete(PackagePath("BDTest", ".snupkg"));

        await AssertRejected("BDTest.1.0.0.snupkg");
    }

    [Test]
    public async Task Rejects_a_symbol_package_with_a_different_version()
    {
        CreatePackage("BDTest", ".snupkg", "2.0.0");

        await AssertRejected("Package/symbol identity mismatch");
    }

    [Test]
    public async Task Rejects_missing_documentation_beside_an_assembly()
    {
        using (var archive = ZipFile.Open(PackagePath("BDTest", ".nupkg"), ZipArchiveMode.Update))
        {
            archive.CreateEntry("lib/netstandard2.0/BDTest.dll");
        }

        await AssertRejected("Missing lib/netstandard2.0/BDTest.xml");
    }

    [Test]
    public async Task Rejects_missing_package_metadata()
    {
        UpdateManifest(metadata => metadata.Element("description")!.Remove());

        await AssertRejected("Missing package metadata description");
    }

    [Test]
    public async Task Rejects_a_missing_license()
    {
        UpdateManifest(metadata => metadata.Element("license")!.Remove());

        await AssertRejected("Missing Apache-2.0 license expression");
    }

    [Test]
    public async Task Rejects_a_missing_icon()
    {
        using (var archive = ZipFile.Open(PackagePath("BDTest", ".nupkg"), ZipArchiveMode.Update))
        {
            archive.GetEntry("nuget_icon.png")!.Delete();
        }

        await AssertRejected("Missing or duplicate package icon");
    }

    [Test]
    public async Task Rejects_a_stale_dependency_version()
    {
        UpdateManifest(metadata => metadata.Descendants("dependency")
            .Single(dependency => (string?)dependency.Attribute("id") == "System.Text.Json")
            .SetAttributeValue("version", "9.0.0"));

        await AssertRejected("Package dependency mismatch for System.Text.Json");
    }

    [TestCase("Microsoft.NET.Test.Sdk", "18.11.0")]
    [TestCase("Humanizer.Core", "3.0.10")]
    public async Task Rejects_an_unexpected_dependency(string id, string version)
    {
        UpdateManifest(metadata => metadata.Element("dependencies")!.Add(
            new XElement("dependency", new XAttribute("id", id), new XAttribute("version", version))));

        await AssertRejected("Unexpected package dependencies");
    }

    [Test]
    public async Task Rejects_redundant_build_folder_assemblies()
    {
        using (var archive = ZipFile.Open(PackagePath("BDTest", ".nupkg"), ZipArchiveMode.Update))
        {
            archive.CreateEntry("build/netstandard2.0/BDTest.dll");
        }

        await AssertRejected("Redundant build-folder assembly");
    }

    private void UpdateManifest(Action<XElement> update)
    {
        using var archive = ZipFile.Open(PackagePath("BDTest", ".nupkg"), ZipArchiveMode.Update);
        var entry = archive.GetEntry("BDTest.nuspec")!;
        XDocument manifest;
        using (var stream = entry.Open())
        {
            manifest = XDocument.Load(stream);
        }

        update(manifest.Root!.Element("metadata")!);
        entry.Delete();
        using var output = archive.CreateEntry("BDTest.nuspec").Open();
        manifest.Save(output);
    }

    private async Task AssertRejected(params string[] expectedMessages)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[] { "-NoProfile", "-File", _scriptPath, "-PackageDirectory", _packageDirectory })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the artifact verification script.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var text = await output + await error;

        Assert.That(process.ExitCode, Is.Not.Zero, text);
        foreach (var message in expectedMessages)
        {
            Assert.That(text, Does.Contain(message));
        }
    }

    private void CreatePackage(string id, string extension, string version = "1.0.0")
    {
        File.Delete(PackagePath(id, extension));
        using var archive = ZipFile.Open(PackagePath(id, extension), ZipArchiveMode.Create);
        var projectPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_scriptPath))!, id, $"{id}.csproj");
        var project = XDocument.Load(projectPath);
        var dependencies = project.Descendants("PackageReference")
            .Where(reference => (string?)reference.Attribute("PrivateAssets") != "all").Select(reference =>
            new XElement("dependency", new XAttribute("id", reference.Attribute("Include")!.Value),
                new XAttribute("version", reference.Attribute("Version")!.Value)))
            .Concat(project.Descendants("ProjectReference").Select(reference =>
                new XElement("dependency", new XAttribute("id", Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/'))),
                    new XAttribute("version", version))));
        var manifest = new XDocument(new XElement("package", new XElement("metadata",
            new XElement("id", id), new XElement("version", version), new XElement("title", id),
            new XElement("authors", "Tom Longhurst"), new XElement("description", "Package verification fixture"),
            new XElement("copyright", "Copyright ©2021 Tom Longhurst"),
            new XElement("projectUrl", "https://github.com/thomhurst/BDTest"), new XElement("tags", "test bdd"),
            new XElement("license", new XAttribute("type", "expression"), "Apache-2.0"),
            new XElement("icon", "nuget_icon.png"),
            new XElement("repository", new XAttribute("type", "git"), new XAttribute("url", "https://github.com/thomhurst/BDTest")),
            new XElement("dependencies", dependencies))));
        using (var stream = archive.CreateEntry($"{id}.nuspec").Open())
        {
            manifest.Save(stream);
        }

        archive.CreateEntry("nuget_icon.png");
        if (id is "BDTest.SourceGenerator" or "BDTest.Analyzers")
        {
            archive.CreateEntry($"analyzers/dotnet/cs/{id}.dll");
        }
    }

    private string PackagePath(string id, string extension) => Path.Combine(_packageDirectory, $"{id}.1.0.0{extension}");
}
