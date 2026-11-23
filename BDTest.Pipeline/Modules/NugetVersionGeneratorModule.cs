using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Context;

namespace BDTest.Pipeline.Modules;

public class NugetVersionGeneratorModule : Module<string>
{
    protected override async Task<string> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        // A published GitHub release ships the version in its tag. Its checkout is a detached tag,
        // where GitVersion resolves no branch, so it must not decide the version.
        var releaseVersion = GetReleaseVersion(
            context.Environment.Variables.Get("GITHUB_EVENT_NAME"),
            context.Environment.Variables.Get("RELEASE_VERSION"));

        if (releaseVersion is not null)
        {
            return releaseVersion;
        }

        var gitVersionInformation = await context.Tools.Git.Versioning.GetVersioningInformationAsync(cancellationToken);

        if (gitVersionInformation.BranchName == "main")
        {
            return gitVersionInformation.SemVer!;
        }

        return $"{gitVersionInformation.Major}.{gitVersionInformation.Minor}.{gitVersionInformation.Patch}-{gitVersionInformation.PreReleaseLabel}-{gitVersionInformation.CommitsSinceVersionSource}";
    }

    /// <summary>
    /// Returns the version a published release ships, or <see langword="null"/> for any other run.
    /// The workflow validates the release tag with <c>scripts/Get-ReleaseVersion.ps1</c>, the
    /// repository's one release-version grammar, and exports it without a <c>v</c> prefix as
    /// <c>RELEASE_VERSION</c>.
    /// </summary>
    public static string? GetReleaseVersion(string? eventName, string? releaseVersion)
    {
        if (eventName != "release")
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(releaseVersion))
        {
            throw new InvalidOperationException(
                "A release run needs RELEASE_VERSION, the version from the release tag.");
        }

        return releaseVersion.Trim();
    }

    protected override async Task<ModuleResult<string>?> OnAfterExecuteAsync(IModuleContext context, ModuleResult<string> result, CancellationToken cancellationToken)
    {
        context.Logger.LogInformation("NuGet Version to Package: {Version}", result.ValueOrDefault);
        return await base.OnAfterExecuteAsync(context, result, cancellationToken);
    }
}
