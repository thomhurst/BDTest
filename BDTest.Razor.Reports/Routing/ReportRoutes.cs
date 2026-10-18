using BDTest.Razor.Reports.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace BDTest.Razor.Reports.Routing;

internal sealed class ReportRoutes : IApplicationModelConvention
{
    internal sealed class AssetMetadata;
    internal string Prefix { get; private set; }
    private bool _initialized;

    internal void Map(string prefix)
    {
        if (Prefix != null || _initialized)
            throw new InvalidOperationException("Call MapBDTestReports once, before mapping other controllers or resolving MVC action descriptors.");
        if (string.IsNullOrWhiteSpace(prefix) || !prefix.StartsWith('/')
            || prefix.TrimEnd('/').Length == 0 || prefix.Contains("//")
            || prefix.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_')))
            throw new ArgumentException("Use a non-root literal path such as /bdtest or /team/reports.", nameof(prefix));
        Prefix = prefix.TrimEnd('/');
    }

    public void Apply(ApplicationModel application)
    {
        _initialized = true;
        foreach (var controller in application.Controllers.Where(controller => IsReportController(controller.ControllerType.AsType())).ToArray())
        {
            if (Prefix == null)
            {
                // Discovery by MapControllers must not expose reports without an explicit mapping.
                application.Controllers.Remove(controller);
                continue;
            }
            foreach (var selector in controller.Selectors)
            {
                selector.AttributeRouteModel = new AttributeRouteModel { Template = Prefix.TrimStart('/') };
                // Host MVC mappings must not publish a second copy of the report endpoints.
                selector.EndpointMetadata.Add(new HostMappingSuppression());
            }
        }
    }

    internal static bool IsReportController(Type type) => type == typeof(BDTestController) || type == typeof(AttachmentsController);

    private sealed class HostMappingSuppression : ISuppressMatchingMetadata, ISuppressLinkGenerationMetadata
    {
        public bool SuppressMatching => true;
        public bool SuppressLinkGeneration => true;
    }

    internal static IEndpointConventionBuilder MapControllers(IEndpointRouteBuilder endpoints)
    {
        // MVC discovers all application parts. Keep its report mapping in a separate data source,
        // and publish only report endpoints so this call cannot expose the host's controllers.
        var reports = new ReportEndpointRouteBuilder(endpoints);
        var controllers = reports.MapControllers();
        controllers.Finally(endpoint =>
        {
            for (var index = endpoint.Metadata.Count - 1; index >= 0; index--)
                if (endpoint.Metadata[index] is HostMappingSuppression)
                    endpoint.Metadata.RemoveAt(index);
        });
        foreach (var source in reports.DataSources)
            endpoints.DataSources.Add(new ReportEndpointDataSource(source));
        return controllers;
    }

    private sealed class ReportEndpointRouteBuilder(IEndpointRouteBuilder host) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => host.ServiceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => host.CreateApplicationBuilder();
    }

    private sealed class ReportEndpointDataSource(EndpointDataSource source) : EndpointDataSource, IDisposable
    {
        public override IReadOnlyList<Endpoint> Endpoints => source.Endpoints.Where(endpoint =>
            endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
            && IsReportController(action.ControllerTypeInfo.AsType())).ToArray();

        public override IChangeToken GetChangeToken() => source.GetChangeToken();
        public void Dispose() => (source as IDisposable)?.Dispose();
    }

    internal sealed class Conventions(IEndpointConventionBuilder controllers, IEndpointConventionBuilder assets) : IEndpointConventionBuilder
    {
        public void Add(Action<EndpointBuilder> convention)
        {
            controllers.Add(endpoint =>
            {
                if (IsReportEndpoint(endpoint)) convention(endpoint);
            });
            assets.Add(convention);
        }

        public void Finally(Action<EndpointBuilder> convention)
        {
            controllers.Finally(endpoint =>
            {
                if (IsReportEndpoint(endpoint)) convention(endpoint);
            });
            assets.Finally(convention);
        }

        private static bool IsReportEndpoint(EndpointBuilder endpoint) => endpoint.Metadata
            .OfType<ControllerActionDescriptor>().Any(action => IsReportController(action.ControllerTypeInfo.AsType()));
    }
}
