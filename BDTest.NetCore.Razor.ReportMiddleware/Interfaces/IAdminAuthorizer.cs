using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace BDTest.NetCore.Razor.ReportMiddleware.Interfaces;

public interface IAdminAuthorizer
{
    Task<bool> IsAdminAsync(HttpContext httpContext);
}