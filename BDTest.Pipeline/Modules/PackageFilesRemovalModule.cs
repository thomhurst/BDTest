using ModularPipelines;
using ModularPipelines.Context;

namespace BDTest.Pipeline.Modules;

public class PackageFilesRemovalModule : Module
{
    protected override async Task ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        new PackageArtifacts((await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.Path).PrepareDirectory();
    }
}
