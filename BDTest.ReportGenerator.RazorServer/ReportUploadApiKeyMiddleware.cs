using System.Security.Cryptography;
using System.Text;
using BDTest.Razor.Reports.Controllers;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace BDTest.ReportGenerator.RazorServer;

// The library leaves uploads anonymous so machines can submit reports without a browser login.
// This host requires an API key before that action can run.
public class ReportUploadApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.ControllerTypeInfo.AsType() == typeof(BDTestController)
            && action.MethodInfo.Name == nameof(BDTestController.Index))
        {
            var expectedKey = configuration["ReportUpload:ApiKey"];
            var suppliedKeys = context.Request.Headers["X-BDTest-Api-Key"];

            if (string.IsNullOrWhiteSpace(expectedKey)
                || suppliedKeys.Count != 1
                || string.IsNullOrEmpty(suppliedKeys[0])
                || !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey)),
                    SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKeys[0]))))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await next(context);
    }
}
