using System;
using Xunit;
using MVCCaseManagement.Utils;

namespace MVCCaseManagement.Tests
{
    public class UrlSecurityValidatorTests
    {
        [Theory]
        [InlineData("https://services.ecourts.gov.in/ecourtindia_v6/")]
        [InlineData("https://delhigw.napix.gov.in/api/v1/case")]
        [InlineData("https://tr18.itnwkrtc.in/api/auth")]
        [InlineData("https://karnatakajudiciary.kar.nic.in/causelist")]
        [InlineData("https://hcservices.ecourts.gov.in/orders/123.pdf")]
        public void IsAllowedExternalUrl_LegitimateDomains_ReturnsTrue(string url)
        {
            bool isAllowed = UrlSecurityValidator.IsAllowedExternalUrl(url, out string? error);
            Assert.True(isAllowed, error);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("http://services.ecourts.gov.in/")] // HTTP insecure
        [InlineData("https://127.0.0.1/admin")]          // Loopback
        [InlineData("https://localhost:5001/status")]     // Localhost
        [InlineData("https://10.0.0.1/credentials")]      // RFC1918 Private Class A
        [InlineData("https://192.168.1.1/router")]        // RFC1918 Private Class C
        [InlineData("https://172.16.0.1/internal")]       // RFC1918 Private Class B
        [InlineData("https://169.254.169.254/metadata")]  // AWS/Cloud Link-Local Metadata
        [InlineData("https://attacker.com/exploit")]      // Unauthorized host
        [InlineData("https://evil-ecourts.gov.in.attacker.org/")] // Spoofed prefix
        [InlineData("ftp://services.ecourts.gov.in/")]    // Non-HTTP/S scheme
        [InlineData("not-a-url")]                         // Malformed URL
        public void IsAllowedExternalUrl_HostileOrUnapprovedUrls_ReturnsFalse(string url)
        {
            bool isAllowed = UrlSecurityValidator.IsAllowedExternalUrl(url, out string? error);
            Assert.False(isAllowed);
            Assert.NotNull(error);
        }
    }
}
