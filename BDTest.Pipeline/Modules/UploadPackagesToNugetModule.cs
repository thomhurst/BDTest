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
        .WithSkipWhen(ShouldSkip, "Packages publish only from a published GitHub release with PUBLISH_PACKAGES=true");

    protected override async Task OnBeforeExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var packagePaths = await context.GetModule<PackagePathsParserModule>();

        foreach (var packagePath in packagePaths.ValueOrDefault!)
        {
            context.Logger.LogInformation("Uploading {File}", packagePath);
        }

        await base.OnBeforeExecuteAsync(context, cancellationToken);
    }

    // Packages ship only from a published GitHub release, which runs on a detached tag checkout.
    // A branch check would resolve no branch there, so the event decides instead. GitHub has
    // already created the tag and the release by the time this runs.
    private static bool ShouldSkip(IModuleContext context)
    {
        if (context.Environment.Variables.Get("GITHUB_EVENT_NAME") != "release")
        {
            return true;
        }

        var publishPackages =
            context.Environment.Variables.Get("PUBLISH_PACKAGES");

        return !bool.TryParse(publishPackages, out var shouldPublishPackages) || !shouldPublishPackages;
    }

    protected override async Task<CommandResult[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_options.Value.ApiKey);

        var packagePaths = await context.GetModule<PackagePathsParserModule>();

        // A failed release is retried by re-running its workflow: SkipDuplicate keeps what already went
        // up, and explicit symbol pushes still run when NuGet skips an already published primary package.
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
