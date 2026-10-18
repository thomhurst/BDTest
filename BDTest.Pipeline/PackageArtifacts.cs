using File = ModularPipelines.FileSystem.File;

namespace BDTest.Pipeline;

public sealed class PackageArtifacts(string repositoryRoot)
{
    public string OutputDirectory { get; } = Path.Combine(repositoryRoot, "artifacts", "packages");

    public void PrepareDirectory()
    {
        Directory.CreateDirectory(OutputDirectory);

        foreach (var path in Directory.EnumerateFiles(OutputDirectory, "*.nupkg")
                     .Concat(Directory.EnumerateFiles(OutputDirectory, "*.snupkg")))
        {
            System.IO.File.Delete(path);
        }
    }

    public List<File> GetPackages()
    {
        var packages = Directory.EnumerateFiles(OutputDirectory, "*.nupkg")
            .Select(path => new File(path))
            .ToList();

        if (packages.Count == 0)
        {
            throw new InvalidOperationException($"No NuGet packages were created in '{OutputDirectory}'.");
        }

        return packages;
    }
}
