using NUnit.Framework;

namespace BDTest.Pipeline.Tests;

public class PackageArtifactsTests
{
    private string _repositoryRoot = null!;
    private PackageArtifacts _artifacts = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), "BDTest package tests", Guid.NewGuid().ToString("N"));
        _artifacts = new PackageArtifacts(_repositoryRoot);
        _artifacts.PrepareDirectory();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_repositoryRoot, recursive: true);

    [Test]
    public void Finds_all_packages_without_reading_console_output_or_including_symbols()
    {
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "First.1.0.0.snupkg"));
        var first = Path.Combine(_artifacts.OutputDirectory, "First.1.0.0.nupkg");
        var second = Path.Combine(_artifacts.OutputDirectory, "Second.1.0.0.nupkg");
        WriteFile(first);
        WriteFile(second);
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "pack.log"));

        Assert.That(_artifacts.GetPackages().Select(file => file.Path), Is.EquivalentTo(new[] { first, second }));
    }

    [Test]
    public void Ignores_packages_outside_the_output_directory()
    {
        var package = Path.Combine(_artifacts.OutputDirectory, "Current.1.0.0.nupkg");
        WriteFile(package);
        WriteFile(Path.Combine(_repositoryRoot, "OtherProject", "bin", "Stale.1.0.0.nupkg"));
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "nested", "Unrelated.1.0.0.nupkg"));

        Assert.That(_artifacts.GetPackages().Select(file => file.Path), Is.EqualTo(new[] { package }));
    }

    [Test]
    public void Preparation_removes_stale_packages_and_symbols_only_from_the_output_directory()
    {
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "Stale.1.0.0.nupkg"));
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "Stale.1.0.0.snupkg"));
        var unrelated = Path.Combine(_artifacts.OutputDirectory, "notes.txt");
        var outside = Path.Combine(_repositoryRoot, "OtherProject", "bin", "Other.1.0.0.nupkg");
        WriteFile(unrelated);
        WriteFile(outside);

        _artifacts.PrepareDirectory();

        Assert.That(Directory.GetFiles(_artifacts.OutputDirectory), Is.EqualTo(new[] { unrelated }));
        Assert.That(File.Exists(outside), Is.True);
    }

    [Test]
    public void Fails_when_no_package_was_created_even_if_symbols_exist()
    {
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "OnlySymbols.1.0.0.snupkg"));

        var exception = Assert.Throws<InvalidOperationException>(() => _artifacts.GetPackages());

        Assert.That(exception?.Message, Does.Contain(_artifacts.OutputDirectory));
    }

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The artifact path must have a parent directory.", nameof(path)));
        File.WriteAllText(path, "test artifact");
    }
}
