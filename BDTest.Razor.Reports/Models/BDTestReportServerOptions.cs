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

        /// <summary>
        /// Enables security headers on /bdtest responses. Disable only when the host supplies its own
        /// compatible policy. Register UseBDTestReportServer before endpoint/authentication middleware.
        /// </summary>
        public bool EnableSecurityHeaders { get; set; } = true;

        /// <summary>The secret required in the X-BDTest-Upload-Key header. Uploads are disabled when unset.</summary>
        public string? UploadApiKey { get; set; }

        /// <summary>Maximum authenticated upload attempts per minute, shared by all clients of this server instance.</summary>
        public int UploadsPerMinute { get; set; } = 60;
}