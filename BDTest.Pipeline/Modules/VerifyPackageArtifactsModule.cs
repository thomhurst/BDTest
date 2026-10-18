using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace BDTest.Pipeline.Modules;

[DependsOn<PackagePathsParserModule>]
public class VerifyPackageArtifactsModule : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var repositoryRoot = context.Git().RootDirectory!.Path;
        var arguments = new List<string>
        {
            "-NoProfile", "-File", Path.Combine(repositoryRoot, "scripts", "Test-PackageArtifacts.ps1"),
            "-PackageDirectory", new PackageArtifacts(repositoryRoot).OutputDirectory
        };

        if (context.Environment.Variables.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            arguments.Add("-RequireNormalizedPaths");
        }

        return await context.Shell.Command.ExecuteCommandLineTool(new GenericCommandLineToolOptions("pwsh")
        {
            Arguments = arguments
        }, new CommandExecutionOptions
        {
            WorkingDirectory = repositoryRoot,
            ThrowOnNonZeroExitCode = true
        }, cancellationToken);
    }
}
