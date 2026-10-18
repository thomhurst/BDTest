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
    public async Task Verifies_imported_packability_and_package_id_independent_of_project_name()
    {
        var root = Path.Combine(_packageDirectory, "fixture");
        var repositoryRoot = Path.GetDirectoryName(Path.GetDirectoryName(_scriptPath))!;
        var scripts = Path.Combine(root, "scripts");
        Directory.CreateDirectory(scripts);
        File.Copy(_scriptPath, Path.Combine(scripts, "Test-PackageArtifacts.ps1"));
        _scriptPath = Path.Combine(scripts, "Test-PackageArtifacts.ps1");
        await File.WriteAllTextAsync(Path.Combine(root, "Shipping.props"), """
            <Project><PropertyGroup>
              <IsPackable Condition="'$(Configuration)' == 'Release'">true</IsPackable>
              <PackageId>Custom.Shipping</PackageId>
            </PropertyGroup></Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "Example.Tests.Pipeline.ReportGenerator.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsRoslynComponent>true</IsRoslynComponent></PropertyGroup>
              <Import Project="Shipping.props" />
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "Library.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable></PropertyGroup>
            </Project>
            """);
        // The verifier reads HEAD for Source Link validation, even for analyzer-only fixtures.
        var (_, gitDirectory) = await RunProcess("git", ["-C", repositoryRoot, "rev-parse", "--absolute-git-dir"]);
        await File.WriteAllTextAsync(Path.Combine(root, ".git"), $"gitdir: {gitDirectory.Trim()}");
        foreach (var package in Directory.EnumerateFiles(_packageDirectory))
        {
            File.Delete(package);
        }
        CreatePackage("Custom.Shipping", ".nupkg");

        var (exitCode, text) = await RunVerifier();
        Assert.That(exitCode, Is.Zero, text);
        Assert.That(text, Does.Contain("Verified Custom.Shipping"));
    }

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

    [TestCase("net8.0")]
    [TestCase(".NETStandard2.0")]
    public async Task Rejects_a_stale_dependency_version(string target)
    {
        UpdateManifest(metadata => metadata.Descendants("group").Single(group => (string?)group.Attribute("targetFramework") == target)
            .Elements("dependency").Single(dependency => (string?)dependency.Attribute("id") == "System.Text.Json")
            .SetAttributeValue("version", "9.0.0"));

        await AssertRejected("Package dependency mismatch for System.Text.Json");
    }

    [Test]
    public async Task Rejects_legacy_only_dependencies_in_modern_group()
    {
        UpdateManifest(metadata => metadata.Descendants("group").Single(group => (string?)group.Attribute("targetFramework") == "net8.0")
            .Add(new XElement("dependency", new XAttribute("id", "Microsoft.Bcl.AsyncInterfaces"), new XAttribute("version", "10.0.13"))));
        await AssertRejected("Unexpected package dependencies", "net8.0");
    }

    [Test]
    public async Task Rejects_a_missing_modern_dependency_group()
    {
        UpdateManifest(metadata => metadata.Descendants("group").Single(group => (string?)group.Attribute("targetFramework") == "net8.0").Remove());
        await AssertRejected("expected one group per target");
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
        var (exitCode, text) = await RunVerifier();
        Assert.That(exitCode, Is.Not.Zero, text);
        foreach (var message in expectedMessages)
        {
            Assert.That(text, Does.Contain(message));
        }
    }

    private Task<(int ExitCode, string Text)> RunVerifier() => RunProcess("pwsh",
        ["-NoProfile", "-File", _scriptPath, "-PackageDirectory", _packageDirectory], assertSuccess: false);

    private static async Task<(int ExitCode, string Text)> RunProcess(string command, string[] arguments, bool assertSuccess = true)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {command}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var text = await output + await error;

        if (assertSuccess)
        {
            Assert.That(process.ExitCode, Is.Zero, text);
        }
        return (process.ExitCode, text);
    }

    private void CreatePackage(string id, string extension, string version = "1.0.0")
    {
        File.Delete(PackagePath(id, extension));
        using var archive = ZipFile.Open(PackagePath(id, extension), ZipArchiveMode.Create);
        var projectPath = id == "Custom.Shipping"
            ? Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_scriptPath))!, "Example.Tests.Pipeline.ReportGenerator.csproj")
            : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_scriptPath))!, id, $"{id}.csproj");
        var project = XDocument.Load(projectPath);
        var targets = project.Descendants().Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(element => element.Value.Split(';'));
        var dependencies = targets.Select(target => new XElement("group",
            new XAttribute("targetFramework", target.Replace("netstandard", ".NETStandard")),
            project.Descendants("PackageReference")
                .Where(reference => (string?)reference.Attribute("PrivateAssets") != "all"
                    && (reference.Attribute("Condition") is not { } condition || condition.Value == $"'$(TargetFramework)' == '{target}'"))
                .Select(reference => new XElement("dependency",
                    new XAttribute("id", reference.Attribute("Include")!.Value),
                    new XAttribute("version", reference.Attribute("Version")!.Value)))
                .Concat(project.Descendants("ProjectReference").Select(reference =>
                    new XElement("dependency", new XAttribute("id", Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/'))),
                        new XAttribute("version", version))))));
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
        if (id is "BDTest.SourceGenerator" or "BDTest.Analyzers" or "Custom.Shipping")
        {
            var assemblyName = id == "Custom.Shipping" ? "Example.Tests.Pipeline.ReportGenerator" : id;
            archive.CreateEntry($"analyzers/dotnet/cs/{assemblyName}.dll");
        }
    }

    private string PackagePath(string id, string extension) => Path.Combine(_packageDirectory, $"{id}.1.0.0{extension}");
}
