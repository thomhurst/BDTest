using BDTest.Pipeline.Settings;
using EnumerableAsyncProcessor.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;

namespace BDTest.Pipeline.Modules;

[DependsOn<RunUnitTestsModule>]
[DependsOn<PackagePathsParserModule>]
[DependsOn<VerifyPackageArtifactsModule>]
public class UploadPackagesToNugetModule : Module<CommandResult[]?>
{
    private readonly IOptions<NuGetSettings> _options;

    public UploadPackagesToNugetModule(IOptions<NuGetSettings> options)
    {
        _options = options;
    }

    protected override void Configure(ModuleConfigurationBuilder module) => module
        .WithSkipWhen(async (context, cancellationToken) => await ShouldSkip(context, cancellationToken));

    protected override async Task OnBeforeExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var packagePaths = await context.GetModule<PackagePathsParserModule>();

        foreach (var packagePath in packagePaths.ValueOrDefault!)
        {
            context.Logger.LogInformation("Uploading {File}", packagePath);
        }

        await base.OnBeforeExecuteAsync(context, cancellationToken);
    }

    private static async Task<bool> ShouldSkip(IModuleContext context, CancellationToken cancellationToken)
    {
        var gitVersionInfo = await context.Tools.Git.Versioning.GetVersioningInformationAsync(cancellationToken);

        if (gitVersionInfo.BranchName != "main")
        {
            return true;
        }

        var publishPackages =
            context.Environment.Variables.Get("PUBLISH_PACKAGES")!;

        if (!bool.TryParse(publishPackages, out var shouldPublishPackages) || !shouldPublishPackages)
        {
            return true;
        }

        return false;
    }

    protected override async Task<CommandResult[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_options.Value.ApiKey);

        var gitVersionInformation = await context.Tools.Git.Versioning.GetVersioningInformationAsync(cancellationToken);

        if (gitVersionInformation.BranchName != "main")
        {
            return null;
        }

        var packagePaths = await context.GetModule<PackagePathsParserModule>();

        // Explicit symbol pushes still run when NuGet skips an already published primary package.
        var uploadPaths = PackageArtifacts.GetUploadPaths(packagePaths.ValueOrDefault!.Select(file => file.Path));

        return await uploadPaths.SelectAsync(async file => await context.Tools.DotNet
            .NuGet
            .PushAsync(new DotNetNuGetPushOptions([file])
            {
                Source = "https://api.nuget.org/v3/index.json",
                NoSymbols = file.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase),
                SkipDuplicate = true,
                ApiKey = _options.Value.ApiKey!,
                SymbolApiKey = _options.Value.ApiKey!
            }, cancellationToken: cancellationToken), cancellationToken: cancellationToken).ProcessOneAtATime();
    }
}
