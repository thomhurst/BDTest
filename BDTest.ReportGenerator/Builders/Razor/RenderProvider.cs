using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.PlatformAbstractions;

namespace BDTest.ReportGenerator.Builders.Razor
{
    public class RenderProvider
    {
        private static readonly RazorViewToStringRenderer _renderer;
        private static readonly ServiceProvider _provider;

        static RenderProvider()
        {
            var services = new ServiceCollection();
            var applicationEnvironment = PlatformServices.Default.Application;
            services.AddSingleton(applicationEnvironment);

            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var environment = new HostingEnvironment
            {
                ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
            };
            services.AddSingleton<IHostingEnvironment>(environment);

            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.FileProviders.Clear();
                options.FileProviders.Add(new PhysicalFileProvider(appDirectory));
                //options.AddBDTestViews();
            });

            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

            var diagnosticSource = new DiagnosticListener("Microsoft.AspNetCore");
            services.AddSingleton<DiagnosticSource>(diagnosticSource);

            services.AddLogging();
            services.AddMvc();
            services.AddSingleton<RazorViewToStringRenderer>();
            _provider = services.BuildServiceProvider();
            _renderer = _provider.GetRequiredService<RazorViewToStringRenderer>();
        }

        public static RazorViewToStringRenderer GetRenderer() => _renderer;
    }
}