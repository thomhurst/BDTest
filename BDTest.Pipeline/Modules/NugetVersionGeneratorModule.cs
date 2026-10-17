using Microsoft.Extensions.Logging;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

// ReSharper disable HeuristicUnreachableCode
#pragma warning disable CS0162

namespace BDTest.Pipeline.Modules;

public class NugetVersionGeneratorModule : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var gitVersionInformation = await context.Git().Versioning.GetGitVersioningInformation();

        if (gitVersionInformation.BranchName == "main")
        {
            return gitVersionInformation.SemVer!;
        }

        return $"{gitVersionInformation.Major}.{gitVersionInformation.Minor}.{gitVersionInformation.Patch}-{gitVersionInformation.PreReleaseLabel}-{gitVersionInformation.CommitsSinceVersionSource}";
    }

    protected override async Task<ModuleResult<string>?> OnAfterExecuteAsync(IModuleContext context, ModuleResult<string> result, CancellationToken cancellationToken)
    {
        context.Logger.LogInformation("NuGet Version to Package: {Version}", result.ValueOrDefault);
        return await base.OnAfterExecuteAsync(context, result, cancellationToken);
    }
}
