using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.Options;

namespace BDTest.Pipeline.Modules;

[DependsOn<PackagePathsParserModule>]
public class VerifyPackageArtifactsModule : Module<CommandResult>
{
    protected override async Task<CommandResult> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var repositoryRoot = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.Path;
        var arguments = new List<string>
        {
            "-NoProfile", "-File", Path.Combine(repositoryRoot, "scripts", "Test-PackageArtifacts.ps1"),
            "-PackageDirectory", new PackageArtifacts(repositoryRoot).OutputDirectory
        };

        if (context.Environment.Variables.Get("GITHUB_ACTIONS") == "true")
        {
            arguments.Add("-RequireNormalizedPaths");
        }

        await context.Shell.RunAsync(new CommandLineToolOptions("pwsh")
        {
            Arguments = arguments
        }, new CommandExecutionOptions
        {
            WorkingDirectory = repositoryRoot,
            ThrowOnNonZeroExitCode = true
        }, cancellationToken);

        // Packing rebuilds assemblies. Validate their final identities before either upload path runs.
        return await context.Shell.RunAsync(new CommandLineToolOptions("pwsh")
        {
            Arguments = ["-NoProfile", "-File", Path.Combine(repositoryRoot, "scripts", "Test-AssemblySigning.ps1")]
        }, new CommandExecutionOptions
        {
            WorkingDirectory = repositoryRoot,
            ThrowOnNonZeroExitCode = true
        }, cancellationToken);
    }
}
