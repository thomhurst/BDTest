using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace BDTest.Pipeline.Modules;

public class PackageFilesRemovalModule : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        new PackageArtifacts(context.Git().RootDirectory!.Path).PrepareDirectory();

        await Task.CompletedTask;
    }
}
