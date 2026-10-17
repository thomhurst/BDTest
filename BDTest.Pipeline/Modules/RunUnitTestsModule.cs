using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace BDTest.Pipeline.Modules;

public class RunUnitTestsModule : Module<List<CommandResult>>
{
    protected override async Task<List<CommandResult>?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();

        foreach (var unitTestProjectFile in context
                     .Git().RootDirectory!
                     .GetFiles(file => file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                                       && file.Path.Contains("Tests", StringComparison.OrdinalIgnoreCase)))
        {
            // TUnit runs on Microsoft.Testing.Platform, which no longer supports the VSTest-based dotnet test on the .NET 10 SDK
            if (unitTestProjectFile.Path.Contains("TUnit", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(await context.DotNet().Run(new DotNetRunOptions
                {
                    Project = unitTestProjectFile.Path,
                    CommandLogging = CommandLogging.Input | CommandLogging.Error,
                }, cancellationToken));
                continue;
            }

            results.Add(await context.DotNet().Test(new DotNetTestOptions
            {
                Arguments = [unitTestProjectFile.Path],
            }, new CommandExecutionOptions
            {
                LogSettings = new CommandLoggingOptions
                {
                    ShowCommandArguments = true,
                    ShowStandardOutput = false,
                    ShowStandardError = true,
                }
            }, cancellationToken));
        }

        return results;
    }
}
