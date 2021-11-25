using BDTest.NetCore.Razor.ReportMiddleware.Implementations;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;
using Microsoft.Extensions.Logging;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class BDTestReportServerOptions
    {
        internal BDTestReportServerOptions()
        {
        }
#nullable enable
        public IBDTestDataStore DataStore { get; set; }
#nullable enable
        public IBDTestCustomSidebarLinksProvider CustomSidebarLinksProvider { get; set; }
#nullable enable
        public IBDTestCustomHeaderLinksProvider CustomHeaderLinksProvider { get; set; }
#nullable enable
        public IAdminAuthorizer AdminAuthorizer { get; set; } = new NoOpAdminAuthorizer();
#nullable enable
        public IBDTestDataReceiver DataReceiver { get; set; }
#nullable enable
        public IUserPersonalizer UserPersonalizer { get; set; }
#nullable enable
        public ILogger? Logger { get; set; }
    }
}