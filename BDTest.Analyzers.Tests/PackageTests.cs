using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BDTest.Analyzers.Tests;

public class PackageTests
{
    [Test]
    public async Task Packed_analyzers_and_generator_reach_a_NuGet_consumer()
    {
        var repository = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (repository != null && !File.Exists(Path.Combine(repository.FullName, "BDTest.sln"))) repository = repository.Parent;
        if (repository == null) throw new InvalidOperationException("Repository root not found");
        var temporary = Path.Combine(Path.GetTempPath(), "bdtest-analyzer-consumer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var feed = Path.Combine(temporary, "feed");
            Directory.CreateDirectory(feed);
            var version = "0.0.0-test-" + Guid.NewGuid().ToString("N");
#if DEBUG
            const string configuration = "Debug";
#else
            const string configuration = "Release";
#endif
            foreach (var package in new[] { "BDTest", "BDTest.Analyzers", "BDTest.SourceGenerator" })
            {
                var packed = await Dotnet(repository.FullName, "pack", package + "/" + package + ".csproj",
                    "-c", configuration, "--no-build", "--no-restore", "-o", feed,
                    "-p:PackageVersion=" + version, "-p:IncludeSymbols=false");
                Assert.That(packed.ExitCode, Is.Zero, packed.Output);
            }
            using (var archive = ZipFile.OpenRead(Path.Combine(feed, "BDTest.Analyzers." + version + ".nupkg")))
            {
                Assert.That(archive.Entries.Any(e => e.FullName == "analyzers/dotnet/cs/BDTest.Analyzers.dll"), Is.True);
                Assert.That(archive.Entries.Any(e => e.FullName.StartsWith("lib/")), Is.False);
                Assert.That(archive.Entries.Any(e => e.Name.StartsWith("Microsoft.CodeAnalysis")), Is.False);
            }
            var sdkFile = Path.Combine(repository.FullName, "global.json");
            if (File.Exists(sdkFile)) File.Copy(sdkFile, Path.Combine(temporary, "global.json"));
            await File.WriteAllTextAsync(Path.Combine(temporary, "Consumer.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <RestoreSources>{{System.Security.SecurityElement.Escape(feed)}};https://api.nuget.org/v3/index.json</RestoreSources>
                    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                    <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="BDTest" Version="{{version}}" />
                    <PackageReference Include="BDTest.Analyzers" Version="{{version}}" PrivateAssets="all" />
                    <PackageReference Include="BDTest.SourceGenerator" Version="{{version}}" PrivateAssets="all" />
                  </ItemGroup>
                </Project>
                """);
            var sourceFile = Path.Combine(temporary, "Consumer.cs");
            await File.WriteAllTextAsync(sourceFile, AnalyzerTests.Source("""
                [StepText("value {0}")] void Step(int value) {}
                Task Test() => Given(() => Step(1)).When(() => Step(2)).Then(() => Step(3)).BDTestAsync();
                """));
            var valid = await Dotnet(temporary, "build", "-c", configuration, "--nologo");
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
            var invalid = await Dotnet(temporary, "build", "-c", configuration, "--no-restore", "--nologo");
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
