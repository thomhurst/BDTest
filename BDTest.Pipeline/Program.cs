using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularPipelines;
using ModularPipelines.Extensions;
using BDTest.Pipeline.Modules;
using BDTest.Pipeline.Modules.LocalMachine;
using BDTest.Pipeline.Settings;

var builder = Pipeline.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

builder.Services.Configure<NuGetSettings>(builder.Configuration.GetSection("NuGet"));

if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddModule<CreateLocalNugetFolderModule>()
        .AddModule<AddLocalNugetSourceModule>()
        .AddModule<UploadPackagesToLocalNuGetModule>();
}
else
{
    builder.Services.AddModule<UploadPackagesToNugetModule>();
}

builder.Services
    .AddModule<RunUnitTestsModule>()
    .AddModule<NugetVersionGeneratorModule>()
    .AddModule<PackProjectsModule>()
    .AddModule<PackageFilesRemovalModule>()
    .AddModule<PackagePathsParserModule>()
    .AddModule<VerifyPackageArtifactsModule>();

await builder.Build().RunAsync();
