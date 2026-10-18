using System.Diagnostics;
using System.IO.Compression;
using NUnit.Framework;

namespace BDTest.Pipeline.Tests;

public class PackageArtifactVerificationTests
{
    private string _packageDirectory;
    private string _scriptPath;

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

        Assert.That(directory, Is.Not.Null, "Cannot locate the repository's artifact verification script.");
        _scriptPath = Path.Combine(directory.FullName, "scripts", "Test-PackageArtifacts.ps1");

        foreach (var id in new[] { "BDTest", "BDTest.NUnit", "BDTest.xUnit", "BDTest.MSTest", "BDTest.TUnit", "BDTest.Razor.Reports" })
        {
            CreatePackage(id, ".nupkg");
            CreatePackage(id, ".snupkg");
        }
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_packageDirectory, recursive: true);

    [Test]
    public async Task Rejects_a_missing_primary_package()
    {
        File.Delete(PackagePath("BDTest", ".nupkg"));

        await AssertRejected("primary packages", "found 5");
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

        using var process = Process.Start(startInfo);
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
        using var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open());
        writer.Write($"<package><metadata><id>{id}</id><version>{version}</version></metadata></package>");
    }

    private string PackagePath(string id, string extension) => Path.Combine(_packageDirectory, $"{id}.1.0.0{extension}");
}
