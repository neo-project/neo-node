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
    ///       ::1          -   IPv6  loopback
    ///       10.0.0.0     -   10.255.255.255  (10/8 prefix)
    ///       127.0.0.0    -   127.255.255.255  (127/8 prefix)
    ///       172.16.0.0   -   172.31.255.255  (172.16/12 prefix)
    ///       192.168.0.0  -   192.168.255.255 (192.168/16 prefix)
    /// </summary>
    /// <param name="ipAddress">Address</param>
    /// <returns>True if it was an internal address</returns>
    public static bool IsInternal(this IPAddress ipAddress)
    {
        // Basic checks
        if (IPAddress.IsLoopback(ipAddress)) return true;
        if (IPAddress.Broadcast.Equals(ipAddress)) return true;
        if (IPAddress.Any.Equals(ipAddress)) return true;
        if (IPAddress.IPv6Any.Equals(ipAddress)) return true;
        if (IPAddress.IPv6Loopback.Equals(ipAddress)) return true;

        // Handle IPv4 mapped into IPv6 (e.g., ::ffff:127.0.0.1)
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        var ip = ipAddress.GetAddressBytes();

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

        // IPv6 Specific Checks
        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Unique Local Address (fc00::/7) -> First byte is 0xFC or 0xFD
            if ((ip[0] & 0xFE) == 0xFC) return true;

            // Link-Local Address (fe80::/10) -> First byte is 0xFE, second byte has top 2 bits set (0x80)
            if (ip[0] == 0xFE && (ip[1] & 0xC0) == 0x80) return true;
        }

        return false;
    }
}
