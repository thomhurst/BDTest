using System.Diagnostics;
using System.Reflection.PortableExecutable;
using NUnit.Framework;

namespace BDTest.Pipeline.Tests;

public class AssemblySigningVerificationTests
{
    private string _repositoryRoot = null!;
    private string _scriptPath = null!;
    private string _assemblyPath = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), "BDTest signing verification", Guid.NewGuid().ToString("N"));
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "scripts", "Test-AssemblySigning.ps1")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Cannot locate the repository's signing verification script.");
        }

        Directory.CreateDirectory(Path.Combine(_repositoryRoot, "scripts"));
        _scriptPath = Path.Combine(_repositoryRoot, "scripts", "Test-AssemblySigning.ps1");
        File.Copy(Path.Combine(directory.FullName, "scripts", "Test-AssemblySigning.ps1"), _scriptPath);
        File.WriteAllText(Path.Combine(_repositoryRoot, "Directory.Build.props"),
            "<Project><PropertyGroup><BDTestPublicKey>test-key</BDTestPublicKey></PropertyGroup></Project>");
        var projectDirectory = Path.Combine(_repositoryRoot, "BDTest.Pipeline.Tests");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "bin", "Release", "net10.0"));
        File.WriteAllText(Path.Combine(projectDirectory, "BDTest.Pipeline.Tests.csproj"),
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        _assemblyPath = Path.Combine(projectDirectory, "bin", "Release", "net10.0", "BDTest.Pipeline.Tests.dll");
        File.Copy(typeof(AssemblySigningVerificationTests).Assembly.Location, _assemblyPath);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_repositoryRoot, recursive: true);

    [Test]
    public async Task Accepts_unsigned_tooling_with_its_expected_identity()
    {
        var (exitCode, output) = await Verify();

        Assert.That(exitCode, Is.Zero, output);
    }

    [Test]
    public async Task Rejects_an_assembly_with_the_wrong_name()
    {
        File.Copy(typeof(PackageArtifacts).Assembly.Location, _assemblyPath, overwrite: true);

        var (exitCode, output) = await Verify();

        Assert.That(exitCode, Is.Not.Zero);
        Assert.That(output, Does.Contain("Unexpected assembly name or public key"));
    }

    [Test]
    public async Task Rejects_an_unsigned_assembly_with_the_signed_flag_set()
    {
        int flagsOffset;
        using (var reader = new PEReader(File.OpenRead(_assemblyPath)))
        {
            // The CorFlags field starts 16 bytes into the CLI header.
            flagsOffset = reader.PEHeaders.CorHeaderStartOffset + 16;
        }

        var bytes = File.ReadAllBytes(_assemblyPath);
        bytes[flagsOffset] |= (byte)CorFlags.StrongNameSigned;
        File.WriteAllBytes(_assemblyPath, bytes);

        var (exitCode, output) = await Verify();

        Assert.That(exitCode, Is.Not.Zero);
        Assert.That(output, Does.Contain("Unexpected strong-name signing flag"));
    }

    private async Task<(int ExitCode, string Output)> Verify()
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(_scriptPath);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start signing verification.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }
}
