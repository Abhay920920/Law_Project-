using System;
using System.Collections.Generic;

namespace MVCCaseManagement.Common
{
    public class ECourtsModuleOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string DeptId { get; set; } = "clonwkrtc";
        public string HmacKey { get; set; } = "15081947";
        public string AuthKey { get; set; } = string.Empty;
        public string IV { get; set; } = string.Empty;
        public string Version { get; set; } = "v1.0";
        public string GatewayUrl { get; set; } = "https://delhigw.napix.gov.in/nic/ecourts";
        public string OAuthTokenUrl { get; set; } = "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token";
        public int HourlyQuota { get; set; } = 1000;
    }

    public class ECourtsOptions
    {
        public const string SectionName = "eCourts";

        public bool EnableBackgroundSync { get; set; } = false;
        public int SyncThrottleSeconds { get; set; } = 3;

        /// <summary>NAPIX App credentials for MVC / MACT claims module.</summary>
        public ECourtsModuleOptions MVC { get; set; } = new ECourtsModuleOptions();

        /// <summary>NAPIX App credentials for Labour disputes & Service Matters module.</summary>
        public ECourtsModuleOptions Labour { get; set; } = new ECourtsModuleOptions();

        /// <summary>NAPIX App credentials for Other Courts module (OS, PSC, CC, Consumer, LAC, ECA).</summary>
        public ECourtsModuleOptions OtherCourts { get; set; } = new ECourtsModuleOptions();

        // Fallback root properties for single-app backwards compatibility
        public string ApiKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string DeptId { get; set; } = string.Empty;
        public string HmacKey { get; set; } = string.Empty;
        public string AuthKey { get; set; } = string.Empty;
        public string IV { get; set; } = string.Empty;
        public string Version { get; set; } = "v1.0";
        public string GatewayUrl { get; set; } = "https://delhigw.napix.gov.in/nic/ecourts";
        public string OAuthTokenUrl { get; set; } = "https://delhigw.napix.gov.in/nic/ecourts/oauth2/token";
        public int HourlyQuota { get; set; } = 1000;
        
        /// <summary>
        /// Whitelisted domains for e-Courts PDF retrieval to prevent SSRF attacks.
        /// </summary>
        public List<string> AllowedDomains { get; set; } = new List<string>
        {
            "delhigw.napix.gov.in",
            "services.ecourts.gov.in",
            "hcservices.ecourts.gov.in",
            "ecourts.gov.in"
        };
    }

    public class SmsOptions
    {
        public const string SectionName = "SMS";

        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SenderId { get; set; } = "NWKRTC";
        public string SecureKey { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = "https://smsmobile1.karnataka.gov.in/index.php/sendmsg";
        public string TemplateId { get; set; } = "1107174825473036218";
    }

    public class TR18Options
    {
        public const string SectionName = "TR18";

        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://tr18.itnwkrtc.in/api/v1/accidents.php";
    }
}
