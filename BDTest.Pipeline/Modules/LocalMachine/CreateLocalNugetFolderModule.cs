using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.FileSystem;

namespace BDTest.Pipeline.Modules.LocalMachine;

[DependsOn<RunUnitTestsModule>]
[DependsOn<PackagePathsParserModule>]
public class CreateLocalNugetFolderModule : Module<FolderPath>
{
    protected override async Task<FolderPath> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var localNugetRepositoryFolder = context.Files.GetFolder(Environment.SpecialFolder.ApplicationData)
            .GetFolder("ModularPipelines")
            .GetFolder("LocalNuget")
            .Create();

        await Task.Yield();

        context.Logger.LogInformation("Local NuGet Repository Path: {Path}", localNugetRepositoryFolder.Path);

        return localNugetRepositoryFolder;
    }
}
