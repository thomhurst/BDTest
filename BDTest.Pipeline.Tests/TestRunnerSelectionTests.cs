using System.Diagnostics;
using BDTest.Pipeline.Modules;

namespace BDTest.Pipeline.Tests;

public class TestRunnerSelectionTests
{
    [Test]
    [Arguments("Renamed.Tests.csproj", "TUnit", true)]
    [Arguments("Other.Tests.csproj", "tunit", true)]
    [Arguments("BDTest.TUnit.Tests.csproj", "xunit.v3.mtp-off", false)]
    [Arguments("Other.TUnit.Tests.csproj", "NUnit", false)]
    [Arguments("Empty.Tests.csproj", "", false)]
    public async Task Runner_follows_package_references_regardless_of_executable_project_name(
        string filename, string package, bool expected)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var project = Path.Combine(root, filename);
            var reference = package.Length == 0 ? "" : $"<PackageReference Include=\"{package}\" Version=\"1.0.0\" />";
            await File.WriteAllTextAsync(project, $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                  </PropertyGroup>
                  <ItemGroup>{{reference}}</ItemGroup>
                </Project>
                """);

            await Assert.That(await UsesTUnitRunner(project)).IsEqualTo(expected);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    [Arguments("Release", false, true)]
    [Arguments("Debug", false, false)]
    [Arguments("Release", true, false)]
    public async Task Runner_honors_imported_Release_conditions_and_removed_references(
        string referenceConfiguration, bool removeReference, bool expected)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "TestRunner.props"), $$"""
                <Project>
                  <ItemGroup Condition="'$(Configuration)' == '{{referenceConfiguration}}'">
                    <PackageReference Include="TUnit" Version="1.0.0" />
                  </ItemGroup>
                </Project>
                """);
            var project = Path.Combine(root, "Imported.Tests.csproj");
            var removal = removeReference ? "<ItemGroup><PackageReference Remove=\"TUnit\" /></ItemGroup>" : "";
            await File.WriteAllTextAsync(project, $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <Import Project="TestRunner.props" />
                  {{removal}}
                </Project>
                """);

            await Assert.That(await UsesTUnitRunner(project)).IsEqualTo(expected);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Repository_selects_TUnit_suites_and_keeps_other_projects_on_VSTest()
    {
        var selected = new List<string>();
        foreach (var project in Directory.EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
                     .Where(path => path.Contains("Tests", StringComparison.OrdinalIgnoreCase)))
        {
            if (await UsesTUnitRunner(project))
            {
                selected.Add(Path.GetFileNameWithoutExtension(project));
            }
        }

        await Assert.That(selected.OrderBy(name => name).ToArray())
            .IsEquivalentTo(new[] { "BDTest.Pipeline.Tests", "BDTest.ReportGenerator.Tests", "BDTest.TUnit.Tests" });
    }

    private static string CreateTemporaryDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "BDTest runner selection TUnit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BDTest.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Cannot locate the repository.");
        }
    }

    private static async Task<bool> UsesTUnitRunner(string projectPath)
    {
        var options = RunUnitTestsModule.CreateRunnerQuery(projectPath);
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryRoot
        };
        foreach (var argument in options.Arguments!)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = await output;
        await Assert.That(process.ExitCode).IsEqualTo(0).Because(result + await error);
        return RunUnitTestsModule.UsesTUnitRunner(result);
    }
}
