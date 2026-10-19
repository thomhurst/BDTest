using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BDTest.Analyzers.Tests;

public class PackageTests
{
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BDTest.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
        }
    }

#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    [OneTimeSetUp]
    public async Task Build_all_core_runtime_assets_for_no_build_packs()
    {
        // A test-project build selects only the nearest core TFM. Package tests need both.
        var built = await Dotnet(RepositoryRoot, "build", "BDTest/BDTest.csproj", "-c", Configuration, "--nologo");
        Assert.That(built.ExitCode, Is.Zero, built.Output);
    }

    [TestCase("BDTest", "netstandard2.0")]
    [TestCase("BDTest", "net8.0")]
    [TestCase("BDTest", "net10.0")]
    [TestCase("BDTest.NUnit", "net8.0")]
    [TestCase("BDTest", "net8.0", "net8.0")]
    [TestCase("BDTest", "netstandard2.0", "netstandard2.0")]
    public async Task Packed_analyzers_and_generator_reach_a_NuGet_consumer(string packageId, string targetFramework, string? packTarget = null)
    {
        var repository = RepositoryRoot;
        var temporary = Path.Combine(Path.GetTempPath(), "bdtest-analyzer-consumer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var feed = Path.Combine(temporary, "feed");
            Directory.CreateDirectory(feed);
            var version = "0.0.0-test-" + Guid.NewGuid().ToString("N");
            foreach (var package in new[] { "BDTest.Contracts", "BDTest", "BDTest.NUnit" })
            {
                var arguments = new List<string>
                {
                    "pack", package + "/" + package + ".csproj",
                    "-c", Configuration, "--no-build", "--no-restore", "-o", feed,
                    "-p:PackageVersion=" + version, "-p:IncludeSymbols=false"
                };
                if (package == "BDTest" && packTarget != null)
                {
                    // Restore matching dependency groups before packing a selected runtime asset.
                    var restored = await Dotnet(repository, "restore", "BDTest/BDTest.csproj",
                        "-p:TargetFrameworks=" + packTarget);
                    Assert.That(restored.ExitCode, Is.Zero, restored.Output);
                    arguments.Add("-p:TargetFrameworks=" + packTarget);
                }
                var packed = await Dotnet(repository, arguments.ToArray());
                Assert.That(packed.ExitCode, Is.Zero, packed.Output);
            }
            using (var archive = ZipFile.OpenRead(Path.Combine(feed, "BDTest." + version + ".nupkg")))
            {
                Assert.That(archive.Entries.Count(e => e.FullName == "analyzers/dotnet/cs/BDTest.SourceGenerator.dll"), Is.EqualTo(1));
                Assert.That(archive.Entries.Any(e => e.FullName.StartsWith("lib/") && e.Name == "BDTest.SourceGenerator.dll"), Is.False);
                Assert.That(archive.Entries.Any(e => e.Name.StartsWith("Microsoft.CodeAnalysis")), Is.False);
                using var manifest = new StreamReader(archive.GetEntry("BDTest.nuspec")!.Open());
                Assert.That(await manifest.ReadToEndAsync(), Does.Not.Contain("id=\"BDTest.Analyzers\"").And.Not.Contain("id=\"BDTest.SourceGenerator\"").And.Not.Contain("id=\"Microsoft.CodeAnalysis"));
                Assert.That(archive.Entries.Count(e => e.FullName == "analyzers/dotnet/cs/BDTest.Analyzers.dll"), Is.EqualTo(1));
                Assert.That(archive.Entries.Any(e => e.FullName.StartsWith("lib/") && e.Name == "BDTest.Analyzers.dll"), Is.False);
                Assert.That(archive.Entries.Any(e => e.FullName == "lib/netstandard2.0/BDTest.dll"), Is.EqualTo(packTarget is null or "netstandard2.0"));
                Assert.That(archive.Entries.Any(e => e.FullName == "lib/net8.0/BDTest.dll"), Is.EqualTo(packTarget is null or "net8.0"));
            }
            Assert.That(Directory.GetFiles(feed, "BDTest.Analyzers.*.nupkg"), Is.Empty);
            var sdkFile = Path.Combine(repository, "global.json");
            if (File.Exists(sdkFile)) File.Copy(sdkFile, Path.Combine(temporary, "global.json"));
            await File.WriteAllTextAsync(Path.Combine(temporary, "Consumer.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{{targetFramework}}</TargetFramework>
                    <LangVersion>latest</LangVersion>
                    <RestoreSources>{{System.Security.SecurityElement.Escape(feed)}};https://api.nuget.org/v3/index.json</RestoreSources>
                    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                    <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="{{packageId}}" Version="{{version}}" />
                  </ItemGroup>
                </Project>
                """);
            var sourceFile = Path.Combine(temporary, "Consumer.cs");
            await File.WriteAllTextAsync(sourceFile, AnalyzerTests.Source("""
                [StepText("value {value}")] void Step(int value) {}
                Task Test() => Given(() => Step(1)).When(() => Step(2)).Then(() => Step(3)).BDTestAsync();
                """));
            var valid = await Dotnet(temporary, "build", "-c", Configuration, "--nologo");
            Assert.That(valid.ExitCode, Is.Zero, valid.Output);
            Assert.That(valid.Output, Does.Not.Match("BDT00[1-4]|AD0001|CS8032"));
            Assert.That(Directory.EnumerateFiles(Path.Combine(temporary, "obj", "generated"), "BDTest.StepMetadata.g.cs", SearchOption.AllDirectories), Is.Not.Empty);

            await File.WriteAllTextAsync(sourceFile, AnalyzerTests.Prefix + """
                [Story] class Consumer : BDTestBuilder {
                    public Consumer(BDTest.BDTestRun run) : base(run) {}
                    [StepText("{1}")] void Step(int value) {}
                    void Test() { Given((Action)(() => Task.Delay(1))); }
                }
                """);
            var invalid = await Dotnet(temporary, "build", "-c", Configuration, "--nologo");
            Assert.That(invalid.ExitCode, Is.Not.Zero, invalid.Output);
            Assert.That(Regex.Matches(invalid.Output, @"(?:warning|error) (BDT00[1-4]):").Select(m => m.Groups[1].Value).Distinct(),
                Is.EquivalentTo(new[] { "BDT001", "BDT002", "BDT003", "BDT004" }), invalid.Output);
            Assert.That(invalid.Output, Does.Not.Match("AD0001|CS8032"));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> Dotnet(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // Nested pack/build calls must not leave reusable MSBuild nodes holding redirected pipes.
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        return (process.ExitCode, await output + await errors);
    }
}
