// Copyright (C) 2015-2026 The Neo Project.
//
// Helper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net;
using System.Net.Sockets;

namespace Neo.Plugins.OracleService;

static class Helper
{
    public static bool IsInternal(this IPHostEntry entry)
    {
        return entry.AddressList.Any(IsInternal);
    }

    /// <summary>
    /// Checks if the specified IP address is an internal, private, loopback, or local address.
    /// Supports IPv4, IPv6, IPv4-mapped IPv6 (::ffff:0:0/96), and 6to4 (2002::/16) encapsulations.
    /// </summary>
    /// <remarks>
    /// Covered ranges:
    /// - Loopback: 127.0.0.0/8, ::1
    /// - Private IPv4: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
    /// - Link-Local / APIPA: 169.254.0.0/16, fe80::/10
    /// - CGNAT: 100.64.0.0/10
    /// - Unique/Site Local IPv6: fc00::/7, fec0::/10
    /// - Wildcards / Broadcast: 0.0.0.0, 255.255.255.255, ::
    /// </remarks>
    /// <param name="ipAddress">The IP address to evaluate.</param>
    /// <returns>True if the address is internal; otherwise, false.</returns>
    public static bool IsInternal(this IPAddress ipAddress)
    {
        // Basic checks
        if (IPAddress.IsLoopback(ipAddress)) return true;
        if (IPAddress.Broadcast.Equals(ipAddress)) return true;
        if (IPAddress.Any.Equals(ipAddress)) return true;
        if (IPAddress.IPv6Any.Equals(ipAddress)) return true;
        if (IPAddress.IPv6Loopback.Equals(ipAddress)) return true;
        if (ipAddress.IsIPv6LinkLocal) return true;
        if (ipAddress.IsIPv6UniqueLocal) return true;
        if (ipAddress.IsIPv6SiteLocal) return true;

        // Handle IPv4 mapped into IPv6 (e.g., ::ffff:127.0.0.1)
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        var ip = ipAddress.GetAddressBytes();

        // Handle 6to4 prefix (2002::/16) embedding an IPv4 address
        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6 && ip[0] == 0x20 && ip[1] == 0x02)
        {
            // Extract the embedded IPv4 bytes (bytes 2 to 5)
            var extractedMipv4 = new byte[] { ip[2], ip[3], ip[4], ip[5] };
            ipAddress = new IPAddress(extractedMipv4);
            ip = ipAddress.GetAddressBytes();
        }

        // IPv4 Specific Checks
        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            return ip[0] switch
            {
                10 or 127 => true,
                169 => ip[1] == 254,                // Link-local (APIPA)
                172 => ip[1] >= 16 && ip[1] < 32,   // Private class B
                192 => ip[1] == 168,                // Private class C
                100 => (ip[1] & 0xC0) == 64,        // CGNAT (100.64.0.0/10)
                _ => false
            };
        }

        return false;
    }
}
