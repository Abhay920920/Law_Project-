using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace MVCCaseManagement.Utils
{
    /// <summary>
    /// Validates external URLs to prevent Server-Side Request Forgery (SSRF) and open redirects.
    /// </summary>
    public static class UrlSecurityValidator
    {
        private static readonly HashSet<string> AllowedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "services.ecourts.gov.in",
            "hcservices.ecourts.gov.in",
            "delhigw.napix.gov.in",
            "ecourts.gov.in",
            "karnatakajudiciary.kar.nic.in",
            "tr18.itnwkrtc.in"
        };

        /// <summary>
        /// Validates that the provided URL belongs to an explicitly allowed government / authorized service domain
        /// and does not resolve to private, loopback, link-local, or cloud metadata IP addresses.
        /// </summary>
        public static bool IsSafeExternalUrl(string url, out Uri? validatedUri)
        {
            validatedUri = null;
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            // Strictly require HTTPS to prevent unencrypted transit, MITM, and plaintext proxying
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            string host = uri.Host.ToLowerInvariant();

            // Host allowlist check
            bool isAllowed = AllowedDomains.Any(domain => 
                host.Equals(domain, StringComparison.OrdinalIgnoreCase) || 
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                return false;
            }

            // Reject IP addresses entered directly as hostnames (e.g. http://127.0.0.1 or http://169.254.169.254)
            if (IPAddress.TryParse(host, out var parsedIp))
            {
                if (IPAddress.IsLoopback(parsedIp) || IsPrivateOrReservedIp(parsedIp))
                {
                    return false;
                }
            }

            // Resolve hostname and ensure no resolved IP is private or loopback (DNS rebinding protection)
            try
            {
                var hostAddresses = Dns.GetHostAddresses(host);
                if (hostAddresses.Length == 0) return false;

                foreach (var ip in hostAddresses)
                {
                    if (IPAddress.IsLoopback(ip) || IsPrivateOrReservedIp(ip))
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            validatedUri = uri;
            return true;
        }

        /// <summary>
        /// Validates that the provided URL belongs to an explicitly allowed government / authorized service domain
        /// and outputs an error reason if validation fails.
        /// </summary>
        public static bool IsAllowedExternalUrl(string url, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                error = "URL cannot be empty.";
                return false;
            }

            if (!IsSafeExternalUrl(url, out _))
            {
                error = "URL is invalid, uses an unapproved scheme/domain, or targets an internal/private address.";
                return false;
            }

            return true;
        }

        private static bool IsPrivateOrReservedIp(IPAddress ip)
        {
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast;
            }

            byte[] b = ip.GetAddressBytes();
            if (b.Length != 4) return true;

            // 10.0.0.0/8 (Private)
            if (b[0] == 10) return true;
            // 172.16.0.0/12 (Private)
            if (b[0] == 172 && (b[1] >= 16 && b[1] <= 31)) return true;
            // 192.168.0.0/16 (Private)
            if (b[0] == 192 && b[1] == 168) return true;
            // 127.0.0.0/8 (Loopback)
            if (b[0] == 127) return true;
            // 169.254.0.0/16 (Link-Local / Cloud Metadata)
            if (b[0] == 169 && b[1] == 254) return true;
            // 0.0.0.0/8 (Current network)
            if (b[0] == 0) return true;
            // 224.0.0.0/4 (Multicast)
            if (b[0] >= 224) return true;

            return false;
        }
    }
}
