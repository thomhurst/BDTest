using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using Microsoft.Extensions.Logging;

namespace BDTest.Razor.Reports.Models;

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