using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using File = ModularPipelines.FileSystem.File;

namespace BDTest.Pipeline.Modules;

[DependsOn<PackageFilesRemovalModule>]
[DependsOn<NugetVersionGeneratorModule>]
public class PackProjectsModule : Module<List<CommandResult>>
{
    protected override async Task<List<CommandResult>?> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();
        var packageVersion = await GetModule<NugetVersionGeneratorModule>();
        var projectFiles = context.Git().RootDirectory!.GetFiles(f => GetProjectsPredicate(f, context));
        foreach (var projectFile in projectFiles)
        {
            results.Add(await context.DotNet().Pack(new DotNetPackOptions { ProjectSolution = projectFile.Path, Configuration = Configuration.Release, Properties = new[] { $"PackageVersion={packageVersion.Value}", $"Version={packageVersion.Value}", } }, cancellationToken));
        }

        return results;
    }

    private bool GetProjectsPredicate(File file, IPipelineContext context)
    {
        var path = file.Path;
        if (!path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.Contains("Tests", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.Contains("Pipeline", StringComparison.InvariantCultureIgnoreCase) 
            || path.Contains("Example", StringComparison.InvariantCultureIgnoreCase)
            || path.Contains("ReportGenerator", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        context.Logger.LogInformation("Found File: {File}", path);
        return true;
    }
}