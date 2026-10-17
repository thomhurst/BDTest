using BDTest.Pipeline.Settings;
using EnumerableAsyncProcessor.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace BDTest.Pipeline.Modules;

[DependsOn<RunUnitTestsModule>]
[DependsOn<PackagePathsParserModule>]
public class UploadPackagesToNugetModule : Module<CommandResult[]>
{
    private readonly IOptions<NuGetSettings> _options;

    public UploadPackagesToNugetModule(IOptions<NuGetSettings> options)
    {
        _options = options;
    }

    protected override ModuleConfiguration Configure() => ModuleConfiguration.Create()
        .WithSkipWhen(async context => await ShouldSkip(context))
        .Build();

    protected override async Task OnBeforeExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var packagePaths = await context.GetModule<PackagePathsParserModule>();

        foreach (var packagePath in packagePaths.ValueOrDefault!)
        {
            context.Logger.LogInformation("Uploading {File}", packagePath);
        }

        await base.OnBeforeExecuteAsync(context, cancellationToken);
    }

    private static async Task<SkipDecision> ShouldSkip(IPipelineContext context)
    {
        var gitVersionInfo = await context.Git().Versioning.GetGitVersioningInformation();

        if (gitVersionInfo.BranchName != "main")
        {
            return true;
        }

        var publishPackages =
            context.Environment.Variables.GetEnvironmentVariable("PUBLISH_PACKAGES")!;

        if (!bool.TryParse(publishPackages, out var shouldPublishPackages) || !shouldPublishPackages)
        {
            return true;
        }

        return false;
    }

    protected override async Task<CommandResult[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_options.Value.ApiKey);

        var gitVersionInformation = await context.Git().Versioning.GetGitVersioningInformation();

        if (gitVersionInformation.BranchName != "main")
        {
            return null;
        }

        var packagePaths = await context.GetModule<PackagePathsParserModule>();

        return await packagePaths.ValueOrDefault!.SelectAsync(async file => await context.DotNet()
            .Nuget
            .Push(new DotNetNugetPushOptions
            {
                Path = file,
                Source = "https://api.nuget.org/v3/index.json",
                ApiKey = _options.Value.ApiKey!
            }, cancellationToken: cancellationToken), cancellationToken: cancellationToken).ProcessOneAtATime();
    }
}
