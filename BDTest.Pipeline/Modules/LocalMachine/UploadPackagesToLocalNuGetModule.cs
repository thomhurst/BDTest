using EnumerableAsyncProcessor.Extensions;
using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace BDTest.Pipeline.Modules.LocalMachine;

[DependsOn<AddLocalNugetSourceModule>]
[DependsOn<PackagePathsParserModule>]
[DependsOn<CreateLocalNugetFolderModule>]
public class UploadPackagesToLocalNuGetModule : Module<CommandResult[]>
{
    protected override async Task OnBeforeExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var packagePaths = await context.GetModule<PackagePathsParserModule>();
        foreach (var packagePath in packagePaths.ValueOrDefault!)
        {
            context.Logger.LogInformation("[Local Directory] Uploading {File}", packagePath);
        }

        await base.OnBeforeExecuteAsync(context, cancellationToken);
    }

    protected override async Task<CommandResult[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var localRepoLocation = await context.GetModule<CreateLocalNugetFolderModule>();
        var packagePaths = await context.GetModule<PackagePathsParserModule>();
        return await packagePaths.ValueOrDefault!.SelectAsync(async file => await context.DotNet()
            .Nuget
            .Push(new DotNetNugetPushOptions
            {
                Path = file,
                Source = localRepoLocation.ValueOrDefault!
            }, cancellationToken: cancellationToken), cancellationToken: cancellationToken).ProcessOneAtATime();    }
}