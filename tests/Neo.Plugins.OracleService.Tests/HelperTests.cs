// Copyright (C) 2015-2026 The Neo Project.
//
// HelperTests.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net;

namespace Neo.Plugins.OracleService.Tests;

[TestClass]
public class HelperTests
{
    [TestMethod]
    // --- IPv4 Loopback & Special ---
    [DataRow("127.0.0.1", true)]
    [DataRow("127.255.255.255", true)]
    [DataRow("0.0.0.0", true)] // IPAddress.Any
    [DataRow("255.255.255.255", true)] // IPAddress.Broadcast

    // --- IPv4 Private Networks ---
    [DataRow("10.0.0.1", true)]
    [DataRow("10.255.255.255", true)]
    [DataRow("172.16.0.1", true)]
    [DataRow("172.31.255.255", true)]
    [DataRow("192.168.1.1", true)]
    [DataRow("192.168.255.255", true)]

    // --- IPv4 Link-Local (APIPA) & CGNAT ---
    [DataRow("169.254.0.1", true)]
    [DataRow("100.64.0.1", true)]
    [DataRow("100.127.255.255", true)]

    // --- IPv6 Loopback & Special ---
    [DataRow("::1", true)]
    [DataRow("::", true)] // IPAddress.IPv6Any
    [DataRow("fe80::1", true)] // Link-Local
    [DataRow("fc00::1", true)] // Unique-Local
    [DataRow("fec0::1", true)] // Site-Local

    // --- IPv4-Mapped IPv6 ---
    [DataRow("::ffff:192.168.1.1", true)]
    [DataRow("::ffff:10.0.0.1", true)]
    [DataRow("::ffff:8.8.8.8", false)] // Public IP mapped

    // --- 6to4 Encapsulated (The fixed bug) ---
    [DataRow("2002:C0A8:0101::", true)] // 6to4 embedding 192.168.1.1
    [DataRow("2002:0A00:0001::", true)] // 6to4 embedding 10.0.0.1
    [DataRow("2002:AC10:0001::", true)] // 6to4 embedding 172.16.0.1
    [DataRow("2002:0808:0808::", false)] // 6to4 embedding 8.8.8.8 (Public)

    // --- Public Addresses (Should be false) ---
    [DataRow("8.8.8.8", false)]
    [DataRow("1.1.1.1", false)]
    [DataRow("172.15.255.255", false)] // Just outside private Class B
    [DataRow("172.32.0.0", false)] // Just outside private Class B
    [DataRow("100.63.255.255", false)] // Just outside CGNAT
    [DataRow("100.128.0.0", false)] // Just outside CGNAT
    [DataRow("2001:db8::", false)] // Documentation address
    public void IsInternal_ValidatesCoreAddressRanges(string ipAddressString, bool expectedResult)
    {
        // Arrange
        var address = IPAddress.Parse(ipAddressString);

        // Act
        var result = address.IsInternal();

        // Assert
        Assert.AreEqual(expectedResult, result, $"Failed for IP: {ipAddressString}");
    }

    [TestMethod]
    public void IsInternal_6to4PrivateAddress_IsBlocked()
    {
        // 6to4 encodes 192.168.1.1 as 2002:C0A8:0101::
        var address = IPAddress.Parse("2002:C0A8:0101::");

        Assert.IsFalse(address.IsIPv4MappedToIPv6);
        Assert.IsTrue(address.IsInternal());
    }

    [TestMethod]
    public void IsInternal_IPHostEntry_ReturnsTrueIfAnyIsInternal()
    {
        // Arrange
        var entry = new IPHostEntry
        {
            AddressList = new[]
            {
                IPAddress.Parse("8.8.8.8"),     // External
                IPAddress.Parse("192.168.1.1")  // Internal
            }
        };

        // Act & Assert
        Assert.IsTrue(entry.IsInternal());
    }

    [TestMethod]
    public void IsInternal_IPHostEntry_ReturnsFalseIfAllAreExternal()
    {
        // Arrange
        var entry = new IPHostEntry
        {
            AddressList = new[]
            {
                IPAddress.Parse("8.8.8.8"),
                IPAddress.Parse("1.1.1.1")
            }
        };

        // Act & Assert
        Assert.IsFalse(entry.IsInternal());
    }
}
