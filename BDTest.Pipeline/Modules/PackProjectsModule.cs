using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace BDTest.Pipeline.Modules;

// Tests rebuild the same bin/obj outputs; packing must not read them concurrently.
[DependsOn<RunUnitTestsModule>]
[DependsOn<PackageFilesRemovalModule>]
[DependsOn<NugetVersionGeneratorModule>]
public class PackProjectsModule : Module<List<CommandResult>>
{
    protected override async Task<List<CommandResult>?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();
        var artifacts = new PackageArtifacts(context.Git().RootDirectory!.Path);
        var packageVersion = await context.GetModule<NugetVersionGeneratorModule>();
        var version = packageVersion.ValueOrDefault
            ?? throw new InvalidOperationException("The package version was not generated.");
        var projectFiles = context.Git().RootDirectory!.GetFiles(
            file => file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        foreach (var projectFile in projectFiles)
        {
            var evaluation = await context.Shell.Command.ExecuteCommandLineTool(
                CreatePackabilityQuery(projectFile.Path), new CommandExecutionOptions
                {
                    WorkingDirectory = context.Git().RootDirectory!.Path,
                    ThrowOnNonZeroExitCode = true
                }, cancellationToken);
            if (!bool.Parse(evaluation.StandardOutput.Trim()))
            {
                continue;
            }

            context.Logger.LogInformation("Packing project: {Project}", projectFile.Path);
            results.Add(await context.DotNet().Pack(CreatePackOptions(
                projectFile.Path, artifacts.OutputDirectory, version),
                cancellationToken: cancellationToken));
        }

        return results;
    }

    public static DotNetPackOptions CreatePackOptions(string projectPath, string outputDirectory, string packageVersion)
    {
        var numericVersion = Version.Parse(packageVersion.Split('-', '+')[0]);
        var fileVersion = new Version(numericVersion.Major, numericVersion.Minor,
            numericVersion.Build, Math.Max(0, numericVersion.Revision)).ToString();
        // Preserve the shipped 2.x identity; stabilize binding starting with the next major.
        var assemblyVersion = numericVersion.Major >= 3 ? $"{numericVersion.Major}.0.0.0" : fileVersion;

        return new DotNetPackOptions
        {
            ProjectSolution = projectPath,
            Configuration = "Release",
            Output = outputDirectory,
            Properties = new KeyValue[]
            {
                ("PackageVersion", packageVersion),
                ("Version", packageVersion),
                ("AssemblyVersion", assemblyVersion),
                ("FileVersion", fileVersion),
                ("InformationalVersion", packageVersion)
            }
        };
    }

    public static GenericCommandLineToolOptions CreatePackabilityQuery(string projectPath) => new("dotnet")
    {
        Arguments = ["msbuild", projectPath, "-nologo", "-property:Configuration=Release", "-getProperty:IsPackable"]
    };
}
