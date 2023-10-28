using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Enums;
using ModularPipelines.Modules;

namespace BDTest.Pipeline.Modules;

public class BuildNetSdkLocatorExecutablesModule : Module<List<CommandResult>>
{
    protected override async Task<List<CommandResult>?> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var projectFile = context.Git().RootDirectory!.GetFiles(f => f.Path.EndsWith("TomLonghurst.Nupendencies.NetSdkLocator.csproj")).First();
        
        var results = new List<CommandResult>();

        results.Add(await context.DotNet().Build(new DotNetBuildOptions
        {
            TargetPath = projectFile,
            Configuration = Configuration.Release,
            Framework = "net48",
            CommandLogging = CommandLogging.Input | CommandLogging.Error,
        }, cancellationToken));
        
        results.Add(await context.DotNet().Build(new DotNetBuildOptions
        {
            TargetPath = projectFile,
            Configuration = Configuration.Release,
            Framework = "net7.0",
            CommandLogging = CommandLogging.Input | CommandLogging.Error,
        }, cancellationToken));

        return results;
    }
}
