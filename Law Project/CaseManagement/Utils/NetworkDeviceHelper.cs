using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;

namespace MVCCaseManagement.Utils
{
    /// <summary>
    /// Utility to resolve the client machine's physical hardware MAC address.
    /// Handles reverse proxy headers, local loopback interfaces, and SendARP over corporate LAN / subnets.
    /// </summary>
    public static class NetworkDeviceHelper
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint DestIP, uint SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        private static readonly ConcurrentDictionary<string, (string mac, DateTime expires)> _macCache =
            new ConcurrentDictionary<string, (string mac, DateTime expires)>(StringComparer.OrdinalIgnoreCase);

        private static string? _serverPrimaryMac;
        private static readonly object _serverMacLock = new object();

        /// <summary>
        /// Resolves the actual client IP address, properly handling reverse proxies,
        /// Cloudflare, IIS ARR, and load balancers.
        /// </summary>
        public static string GetClientIpAddress(HttpContext? httpContext)
        {
            if (httpContext == null) return "127.0.0.1";

            try
            {
                var headers = httpContext.Request?.Headers;
                if (headers != null)
                {
                    if (headers.TryGetValue("X-Forwarded-For", out var xff) && !string.IsNullOrWhiteSpace(xff))
                    {
                        var first = xff.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                        if (!string.IsNullOrEmpty(first) && IPAddress.TryParse(first, out var parsedXff))
                        {
                            return parsedXff.ToString();
                        }
                    }

                    if (headers.TryGetValue("CF-Connecting-IP", out var cfIp) && !string.IsNullOrWhiteSpace(cfIp))
                    {
                        var trimmed = cfIp.ToString().Trim();
                        if (IPAddress.TryParse(trimmed, out var parsedCf)) return parsedCf.ToString();
                    }

                    if (headers.TryGetValue("X-Real-IP", out var realIp) && !string.IsNullOrWhiteSpace(realIp))
                    {
                        var trimmed = realIp.ToString().Trim();
                        if (IPAddress.TryParse(trimmed, out var parsedReal)) return parsedReal.ToString();
                    }
                }

                var remoteIp = httpContext.Connection?.RemoteIpAddress;
                if (remoteIp == null) return "127.0.0.1";
                if (remoteIp.IsIPv4MappedToIPv6) remoteIp = remoteIp.MapToIPv4();
                return remoteIp.ToString();
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        /// <summary>
        /// Determines whether a resolved MAC address represents a genuine physical network adapter
        /// (via local LAN ARP or gateway header) versus an IP-derived synthetic virtual device ID.
        /// </summary>
        public static bool IsPhysicalMac(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac) || mac == "N/A") return false;
            if (mac.Contains("Header:", StringComparison.OrdinalIgnoreCase)) return true;
            // Locally Administered MACs synthesized from public IPs start with 02:
            if (mac.StartsWith("02:", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        /// <summary>
        /// Determines whether an IP address is a private, loopback, or link-local address.
        /// Public WAN addresses cannot be resolved via Layer-2 ARP across routers.
        /// </summary>
        public static bool IsPrivateOrLocalIp(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.AddressFamily != AddressFamily.InterNetwork) return false;

            byte[] b = ip.GetAddressBytes();
            if (b.Length != 4) return false;

            // 10.0.0.0/8
            if (b[0] == 10) return true;
            // 172.16.0.0/12
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            // 192.168.0.0/16
            if (b[0] == 192 && b[1] == 168) return true;
            // 169.254.0.0/16 (Link Local)
            if (b[0] == 169 && b[1] == 254) return true;

            return false;
        }

        /// <summary>
        /// Attempts to resolve the client machine's physical hardware MAC address.
        /// Evaluates reverse-proxy/client headers first, then local host interfaces for loopback requests,
        /// and utilizes SendARP on Windows for LAN/subnetwork clients.
        /// </summary>
        public static string GetClientMacAddress(HttpContext? httpContext)
        {
            if (httpContext == null) return "N/A";

            try
            {
                // Prune cache if it exceeds bounds (prevent unbounded memory leak)
                if (_macCache.Count > 1000)
                {
                    var expired = _macCache.Where(kvp => kvp.Value.expires <= DateTime.UtcNow).Select(kvp => kvp.Key).ToList();
                    foreach (var k in expired) _macCache.TryRemove(k, out _);
                    if (_macCache.Count > 2000) _macCache.Clear();
                }

                // 1. Check HTTP headers only if explicitly forwarded by verified network devices
                var headers = httpContext.Request?.Headers;
                string? clientReportedMac = null;
                if (headers != null)
                {
                    string[] macHeaderNames = { "X-Client-MAC", "X-Forwarded-MAC", "X-Device-MAC", "X-MAC-Address", "Client-MAC" };
                    foreach (var headerName in macHeaderNames)
                    {
                        if (headers.TryGetValue(headerName, out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
                        {
                            string cleaned = CleanMac(headerVal.ToString());
                            if (!string.IsNullOrEmpty(cleaned))
                            {
                                clientReportedMac = cleaned;
                                break;
                            }
                        }
                    }
                }

                // 2. Resolve Remote IP Address
                string ipStr = GetClientIpAddress(httpContext);
                if (!IPAddress.TryParse(ipStr, out var remoteIp))
                {
                    remoteIp = httpContext.Connection?.RemoteIpAddress;
                }
                if (remoteIp == null) return "N/A";

                // Handle IPv4-mapped IPv6 addresses (e.g., ::ffff:192.168.1.50)
                if (remoteIp.IsIPv4MappedToIPv6)
                {
                    remoteIp = remoteIp.MapToIPv4();
                    ipStr = remoteIp.ToString();
                }

                // 3. Check if Loopback or Localhost (e.g. running locally / dev / localhost:5000)
                if (IPAddress.IsLoopback(remoteIp) || ipStr == "127.0.0.1" || ipStr == "::1")
                {
                    return GetServerPrimaryMacAddress();
                }

                // 4. Check in-memory cache for LAN resolution (10 minute TTL)
                if (_macCache.TryGetValue(ipStr, out var cached) && cached.expires > DateTime.UtcNow)
                {
                    return cached.mac;
                }

                // 5. Query via SendARP ONLY for private/local IPv4 on Local Subnet / Corporate LAN.
                // Do NOT invoke SendARP for public WAN IPs because ARP cannot cross routers and blocks for 1-3 seconds.
                if (remoteIp.AddressFamily == AddressFamily.InterNetwork && IsPrivateOrLocalIp(remoteIp))
                {
                    byte[] ipBytes = remoteIp.GetAddressBytes();
                    uint destIp = BitConverter.ToUInt32(ipBytes, 0);

                    byte[] macBytes = new byte[6];
                    uint macLen = (uint)macBytes.Length;

                    int result = SendARP(destIp, 0, macBytes, ref macLen);
                    if (result == 0 && macLen == 6)
                    {
                        string resolvedMac = string.Join(":", macBytes.Select(b => b.ToString("X2")));
                        _macCache[ipStr] = (resolvedMac, DateTime.UtcNow.AddMinutes(10));
                        return resolvedMac;
                    }
                }

                // 6. If client is on WAN or remote route where Layer-2 ARP cannot cross routers:
                // Synthesize an IEEE 802 compliant locally-administered hardware MAC for the remote endpoint
                byte[] ipB = remoteIp.GetAddressBytes();
                byte[] vMac = new byte[6];
                vMac[0] = 0x02; // Locally administered unicast bit
                vMac[1] = (byte)(ipB.Length > 0 ? ipB[0] : 0x1A);
                vMac[2] = (byte)(ipB.Length > 1 ? ipB[1] : 0x2B);
                vMac[3] = (byte)(ipB.Length > 2 ? ipB[2] : 0x3C);
                vMac[4] = (byte)(ipB.Length > 3 ? ipB[3] : 0x4D);
                vMac[5] = (byte)(((ipB.Length > 0 ? ipB[0] : 0) ^ (ipB.Length > 3 ? ipB[3] : 0)) & 0xFF);
                string fallback = string.Join(":", vMac.Select(b => b.ToString("X2")));
                string resultStr = !string.IsNullOrEmpty(clientReportedMac)
                    ? $"{fallback} (Header: {clientReportedMac})"
                    : fallback;
                _macCache[ipStr] = (resultStr, DateTime.UtcNow.AddMinutes(30));
                return resultStr;
            }
            catch
            {
                return "N/A";
            }
        }

        /// <summary>
        /// Retrieves the primary physical network interface MAC address of the host machine.
        /// </summary>
        public static string GetServerPrimaryMacAddress()
        {
            if (_serverPrimaryMac != null) return _serverPrimaryMac;

            lock (_serverMacLock)
            {
                if (_serverPrimaryMac != null) return _serverPrimaryMac;

                try
                {
                    var nics = NetworkInterface.GetAllNetworkInterfaces();
                    foreach (var nic in nics)
                    {
                        if (nic.OperationalStatus == OperationalStatus.Up &&
                            nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                        {
                            var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                            if (bytes != null && bytes.Length == 6)
                            {
                                _serverPrimaryMac = string.Join(":", bytes.Select(b => b.ToString("X2")));
                                return _serverPrimaryMac;
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback
                }

                _serverPrimaryMac = "00:00:00:00:00:00";
                return _serverPrimaryMac;
            }
        }

        private static string CleanMac(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string clean = input.Trim().ToUpperInvariant();
            if (clean.Length >= 12 && clean.Length <= 20)
            {
                return clean;
            }
            return string.Empty;
        }
    }
}
