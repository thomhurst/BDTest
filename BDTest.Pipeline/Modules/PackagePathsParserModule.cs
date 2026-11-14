using ModularPipelines;
using ModularPipelines.Context;
using File = ModularPipelines.FileSystem.FilePath;

namespace BDTest.Pipeline.Modules;

[DependsOn<PackProjectsModule>]
public class PackagePathsParserModule : Module<List<File>>
{
    protected override async Task<List<File>> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        await context.GetModule<PackProjectsModule>();

        return new PackageArtifacts((await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.Path).GetPackages();
    }
}
