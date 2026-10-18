using System.Diagnostics;
using System.Text.Json;
using BDTest.Pipeline.Modules;
using NUnit.Framework;

namespace BDTest.Pipeline.Tests;

public class PackageSelectionTests
{
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BDTest.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Cannot locate the repository.");
        }
    }

    [Test]
    public async Task Packing_selects_the_complete_shipped_package_set()
    {
        var selected = new List<string>();
        foreach (var project in Directory.EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var options = PackProjectsModule.CreatePackabilityQuery(project);
            if (bool.Parse(await RunDotNet(options.Arguments!)))
            {
                selected.Add(Path.GetFileNameWithoutExtension(project));
            }
        }

        Assert.That(selected, Is.EquivalentTo(new[]
        {
            "BDTest", "BDTest.NUnit", "BDTest.xUnit", "BDTest.MSTest", "BDTest.TUnit",
            "BDTest.Razor.Reports", "BDTest.SourceGenerator", "BDTest.Analyzers"
        }));
    }

    [Test]
    public async Task Selection_and_metadata_follow_evaluated_opt_in_regardless_of_project_name()
    {
        var root = Path.Combine(Path.GetTempPath(), "BDTest packaging policy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.Copy(Path.Combine(RepositoryRoot, "Directory.Build.props"), Path.Combine(root, "Directory.Build.props"));
            Directory.CreateDirectory(Path.Combine(root, "build"));
            File.Copy(Path.Combine(RepositoryRoot, "build", "PackageMetadata.targets"), Path.Combine(root, "build", "PackageMetadata.targets"));
            await File.WriteAllTextAsync(Path.Combine(root, "Shipping.props"),
                "<Project><PropertyGroup><IsPackable>true</IsPackable></PropertyGroup></Project>");
            var shippingProject = Path.Combine(root, "Example.Tests.ReportGenerator.csproj");
            await File.WriteAllTextAsync(shippingProject,
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><Import Project=\"Shipping.props\" /></Project>");
            var privateProject = Path.Combine(root, "Library.csproj");
            await File.WriteAllTextAsync(privateProject,
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

            Assert.That(bool.Parse(await RunDotNet(PackProjectsModule.CreatePackabilityQuery(shippingProject).Arguments!)), Is.True);
            Assert.That(bool.Parse(await RunDotNet(PackProjectsModule.CreatePackabilityQuery(privateProject).Arguments!)), Is.False);
            using var shipping = await QueryMetadata(shippingProject);
            using var privateLibrary = await QueryMetadata(privateProject);
            Assert.Multiple(() =>
            {
                Assert.That(shipping.RootElement.GetProperty("Properties").GetProperty("PackageIcon").GetString(), Is.EqualTo("nuget_icon.png"));
                Assert.That(privateLibrary.RootElement.GetProperty("Properties").GetProperty("PackageIcon").GetString(), Is.Empty);
                Assert.That(shipping.RootElement.GetProperty("Items").GetProperty("None").EnumerateArray()
                    .Any(item => item.TryGetProperty("Pack", out var pack) && pack.GetString() == "true"), Is.True);
                Assert.That(privateLibrary.RootElement.GetProperty("Items").GetProperty("None").EnumerateArray()
                    .Any(item => item.TryGetProperty("Pack", out var pack) && pack.GetString() == "true"), Is.False);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestCase("BDTest.SourceGenerator")]
    [TestCase("BDTest.Analyzers")]
    public async Task Compiler_packages_keep_analyzer_assets_and_disable_symbols(string projectName)
    {
        using var metadata = await QueryMetadata(Path.Combine(RepositoryRoot, projectName, projectName + ".csproj"));
        var properties = metadata.RootElement.GetProperty("Properties");
        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("TargetFramework").GetString(), Is.EqualTo("netstandard2.0"));
            Assert.That(properties.GetProperty("IncludeBuildOutput").GetString(), Is.EqualTo("false"));
            Assert.That(properties.GetProperty("IncludeSymbols").GetString(), Is.EqualTo("false"));
            Assert.That(properties.GetProperty("PackageIcon").GetString(), Is.EqualTo("nuget_icon.png"));
            Assert.That(properties.GetProperty("Copyright").GetString(), Is.EqualTo("Copyright © Tom Longhurst"));
            Assert.That(metadata.RootElement.GetProperty("Items").GetProperty("None").EnumerateArray()
                .Any(item => item.TryGetProperty("PackagePath", out var path) && path.GetString() == "analyzers/dotnet/cs"), Is.True);
        });
    }

    [TestCase("BDTest", "netstandard2.0")]
    [TestCase("BDTest", "net8.0")]
    [TestCase("BDTest.Razor.Reports", "net10.0")]
    public async Task Runtime_packages_initialize_documentation_before_sdk_defaults(string projectName, string target)
    {
        using var metadata = JsonDocument.Parse(await RunDotNet(new[]
        {
            "msbuild", Path.Combine(RepositoryRoot, projectName, projectName + ".csproj"), "-nologo",
            "-property:Configuration=Release", "-property:TargetFramework=" + target,
            "-getProperty:DocumentationFile,IncludeSymbols,Copyright"
        }));
        var properties = metadata.RootElement.GetProperty("Properties");
        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("DocumentationFile").GetString(), Does.EndWith(projectName + ".xml"));
            Assert.That(properties.GetProperty("IncludeSymbols").GetString(), Is.EqualTo("true"));
            Assert.That(properties.GetProperty("Copyright").GetString(), Is.EqualTo("Copyright © Tom Longhurst"));
        });
    }

    private static async Task<JsonDocument> QueryMetadata(string project) => JsonDocument.Parse(await RunDotNet(new[]
    {
        "msbuild", project, "-nologo", "-property:Configuration=Release",
        "-getProperty:TargetFramework,IncludeBuildOutput,IncludeSymbols,PackageIcon,Copyright", "-getItem:None"
    }));

    private static async Task<string> RunDotNet(IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryRoot
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = await output;
        Assert.That(process.ExitCode, Is.Zero, result + await error);
        return result.Trim();
    }
}
