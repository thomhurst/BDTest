using System.Threading.Tasks;
using BDTest.NetCore.Razor.ReportMiddleware.Extensions;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace BDTest.ReportGenerator.RazorServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllersWithViews()
                .AddBdTestReportControllersAndViews();
            
            services.AddSingleton<AzureStorageDataStore>();
            services.AddSingleton<CustomSidebarLinkProvider>();
            services.AddSingleton<CustomHeaderProvider>();
            services.AddSingleton<AdminAuthorizer>();

            AddConfig(services);

            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    const int durationInSeconds = 60 * 60 * 24;
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                        "public,max-age=" + durationInSeconds;
                }
            });
            
            app.UseBDTestReportServer(options =>
            {
                options.DataStore = app.ApplicationServices.GetRequiredService<AzureStorageDataStore>();
                options.CustomSidebarLinksProvider = app.ApplicationServices.GetRequiredService<CustomSidebarLinkProvider>();
                options.CustomHeaderLinksProvider = app.ApplicationServices.GetRequiredService<CustomHeaderProvider>();
                options.AdminAuthorizer = app.ApplicationServices.GetRequiredService<AdminAuthorizer>();
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });

            Task.Run(() => app.ApplicationServices.GetRequiredService<IDataRepository>().GetAllTestRunRecords());
        }

        private void AddConfig(IServiceCollection services)
        {
            var config = Configuration.Get<Config>();
            
            services.AddSingleton(config.AzureStorage);
        }
    }
}