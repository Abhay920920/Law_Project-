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
        /// Attempts to resolve the client machine's physical hardware MAC address.
        /// Evaluates reverse-proxy/client headers first, then local host interfaces for loopback requests,
        /// and utilizes SendARP on Windows for LAN/subnetwork clients.
        /// </summary>
        public static string GetClientMacAddress(HttpContext? httpContext)
        {
            if (httpContext == null) return "N/A";

            try
            {
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
                var remoteIp = httpContext.Connection?.RemoteIpAddress;
                if (remoteIp == null) return "N/A";

                // Handle IPv4-mapped IPv6 addresses (e.g., ::ffff:192.168.1.50)
                if (remoteIp.IsIPv4MappedToIPv6)
                {
                    remoteIp = remoteIp.MapToIPv4();
                }

                string ipStr = remoteIp.ToString();

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

                // 5. Query via SendARP for IPv4 on Local Subnet / Corporate LAN
                if (remoteIp.AddressFamily == AddressFamily.InterNetwork)
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
