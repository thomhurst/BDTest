namespace BDTest.Pipeline.Tests;

public class PackageArtifactsTests
{
    private string _repositoryRoot = null!;
    private PackageArtifacts _artifacts = null!;

    [Before(HookType.Test)]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), "BDTest package tests", Guid.NewGuid().ToString("N"));
        _artifacts = new PackageArtifacts(_repositoryRoot);
        _artifacts.PrepareDirectory();
    }

    [After(HookType.Test)]
    public void TearDown() => Directory.Delete(_repositoryRoot, recursive: true);

    [Test]
    public async Task Finds_all_packages_without_reading_console_output_or_including_symbols()
    {
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "First.1.0.0.snupkg"));
        var first = Path.Combine(_artifacts.OutputDirectory, "First.1.0.0.nupkg");
        var second = Path.Combine(_artifacts.OutputDirectory, "Second.1.0.0.nupkg");
        WriteFile(first);
        WriteFile(second);
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "pack.log"));

        await Assert.That(_artifacts.GetPackages().Select(file => file.Path)).IsEquivalentTo(new[] { first, second });
    }

    [Test]
    public async Task Ignores_packages_outside_the_output_directory()
    {
        var package = Path.Combine(_artifacts.OutputDirectory, "Current.1.0.0.nupkg");
        WriteFile(package);
        WriteFile(Path.Combine(_repositoryRoot, "OtherProject", "bin", "Stale.1.0.0.nupkg"));
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "nested", "Unrelated.1.0.0.nupkg"));

        await Assert.That(_artifacts.GetPackages().Select(file => file.Path).SequenceEqual(new[] { package })).IsTrue();
    }

    [Test]
    public async Task Preparation_removes_stale_packages_and_symbols_only_from_the_output_directory()
    {
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "Stale.1.0.0.nupkg"));
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "Stale.1.0.0.snupkg"));
        var unrelated = Path.Combine(_artifacts.OutputDirectory, "notes.txt");
        var outside = Path.Combine(_repositoryRoot, "OtherProject", "bin", "Other.1.0.0.nupkg");
        WriteFile(unrelated);
        WriteFile(outside);

        _artifacts.PrepareDirectory();

        await Assert.That(Directory.GetFiles(_artifacts.OutputDirectory).SequenceEqual(new[] { unrelated })).IsTrue();
        await Assert.That(File.Exists(outside)).IsTrue();
    }

    [Test]
    public async Task Fails_when_no_package_was_created_even_if_symbols_exist()
    {
        WriteFile(Path.Combine(_artifacts.OutputDirectory, "OnlySymbols.1.0.0.snupkg"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            _artifacts.GetPackages();
            return Task.CompletedTask;
        });

        await Assert.That(exception?.Message).Contains(_artifacts.OutputDirectory);
    }

    [Test]
    public async Task Uploads_library_symbols_and_analyzer_packages_without_missing_symbol_paths()
    {
        var library = Path.Combine(_artifacts.OutputDirectory, "Library.1.0.0.nupkg");
        var symbols = Path.ChangeExtension(library, ".snupkg");
        var analyzer = Path.Combine(_artifacts.OutputDirectory, "Generator.1.0.0.nupkg");
        WriteFile(library);
        WriteFile(symbols);
        WriteFile(analyzer);

        await Assert.That(PackageArtifacts.GetUploadPaths(new[] { library, analyzer })
            .SequenceEqual(new[] { library, symbols, analyzer })).IsTrue();
    }

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The artifact path must have a parent directory.", nameof(path)));
        File.WriteAllText(path, "test artifact");
    }
}
