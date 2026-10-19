using System.Text.Json;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace BDTest.Pipeline.Modules;

public class RunUnitTestsModule : Module<List<CommandResult>>
{
    protected override async Task<List<CommandResult>?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();

        foreach (var unitTestProjectFile in context
                     .Git().RootDirectory!
                     .GetFiles(file => file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                                       && file.Path.Contains("Tests", StringComparison.OrdinalIgnoreCase)))
        {
            var resultsDirectory = Path.Combine(context.Git().RootDirectory!.Path, "artifacts", "test-results",
                Path.GetFileNameWithoutExtension(unitTestProjectFile.Path));

            // Evaluate imports and Release conditions just as the test invocation does.
            var evaluation = await context.Shell.Command.ExecuteCommandLineTool(
                CreateRunnerQuery(unitTestProjectFile.Path), new CommandExecutionOptions
                {
                    WorkingDirectory = context.Git().RootDirectory!.Path,
                    ThrowOnNonZeroExitCode = true
                }, cancellationToken);
            if (UsesMicrosoftTestingPlatform(evaluation.StandardOutput))
            {
                results.Add(await context.DotNet().Run(new DotNetRunOptions
                {
                    Project = unitTestProjectFile.Path,
                    RunSettings = ["--report-trx", "--results-directory", resultsDirectory],
                    Configuration = "Release",
                }, new CommandExecutionOptions
                {
                    LogSettings = new CommandLoggingOptions
                    {
                        ShowCommandArguments = true,
                        ShowStandardOutput = false,
                        ShowStandardError = true,
                    }
                }, cancellationToken));
                continue;
            }

            results.Add(await context.DotNet().Test(new DotNetTestOptions
            {
                Arguments = [unitTestProjectFile.Path, "--logger", "trx"],
                ResultsDirectory = resultsDirectory,
                Configuration = "Release",
            }, new CommandExecutionOptions
            {
                LogSettings = new CommandLoggingOptions
                {
                    ShowCommandArguments = true,
                    ShowStandardOutput = false,
                    ShowStandardError = true,
                }
            }, cancellationToken));
        }

        return results;
    }

    public static GenericCommandLineToolOptions CreateRunnerQuery(string projectPath) => new("dotnet")
    {
        Arguments = ["msbuild", projectPath, "-nologo", "-property:Configuration=Release", "-getItem:PackageReference", "-getProperty:UseMicrosoftTestingPlatformRunner"]
    };

    public static bool UsesMicrosoftTestingPlatform(string evaluatedProject)
    {
        using var evaluation = JsonDocument.Parse(evaluatedProject);
        var useMtp = string.Equals(evaluation.RootElement.GetProperty("Properties")
            .GetProperty("UseMicrosoftTestingPlatformRunner").GetString(), "true", StringComparison.OrdinalIgnoreCase);
        return evaluation.RootElement.GetProperty("Items").GetProperty("PackageReference")
            .EnumerateArray()
            .Any(package =>
            {
                var name = package.GetProperty("Identity").GetString();
                return string.Equals(name, "TUnit", StringComparison.OrdinalIgnoreCase)
                    || (useMtp && (string.Equals(name, "xunit.v3", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "xunit.v3.mtp-v2", StringComparison.OrdinalIgnoreCase)));
            });
    }
}
