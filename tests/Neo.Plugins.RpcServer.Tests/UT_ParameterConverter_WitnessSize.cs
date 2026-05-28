// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ParameterConverter_WitnessSize.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.Plugins.RpcServer.Tests;

[TestClass]
public class UT_ParameterConverter_WitnessSize
{
    private const int MaxVerificationScriptLength = 1024;
    private const int MaxInvocationScriptLength = 1024;
    private readonly byte _addressVersion = TestProtocolSettings.Default.AddressVersion;

    [TestMethod]
    public void TestWitnessSize_ValidInvocationScript_MaxLength()
    {
        // Arrange: Create a witness with invocation script at max length
        var invocationScript = new byte[MaxInvocationScriptLength];
        var invocationBase64 = Convert.ToBase64String(invocationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = ""
            }
        };

        // Act
        var result = json.ToSignersAndWitnesses(_addressVersion);

        // Assert
        Assert.HasCount(1, result.Signers);
        Assert.HasCount(1, result.Witnesses);
        Assert.AreEqual(MaxInvocationScriptLength, result.Witnesses[0].InvocationScript.Length);
    }

    [TestMethod]
    public void TestWitnessSize_InvalidInvocationScript_ExceedsMaxLength()
    {
        // Arrange: Create a witness with invocation script exceeding max length
        var invocationScript = new byte[MaxInvocationScriptLength + 1];
        var invocationBase64 = Convert.ToBase64String(invocationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = ""
            }
        };

        // Act & Assert
        Assert.ThrowsExactly<RpcException>(
            () => json.ToSignersAndWitnesses(_addressVersion));
    }

    [TestMethod]
    public void TestWitnessSize_ValidVerificationScript_MaxLength()
    {
        // Arrange: Create a witness with verification script at max length
        var verificationScript = new byte[MaxVerificationScriptLength];
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = "",
                ["verification"] = verificationBase64
            }
        };

        // Act
        var result = json.ToSignersAndWitnesses(_addressVersion);

        // Assert
        Assert.HasCount(1, result.Signers);
        Assert.HasCount(1, result.Witnesses);
        Assert.AreEqual(MaxVerificationScriptLength, result.Witnesses[0].VerificationScript.Length);
    }

    [TestMethod]
    public void TestWitnessSize_InvalidVerificationScript_ExceedsMaxLength()
    {
        // Arrange: Create a witness with verification script exceeding max length
        var verificationScript = new byte[MaxVerificationScriptLength + 1];
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = "",
                ["verification"] = verificationBase64
            }
        };

        // Act & Assert
        Assert.ThrowsExactly<RpcException>(
            () => json.ToSignersAndWitnesses(_addressVersion));
    }

    [TestMethod]
    public void TestWitnessSize_BothScripts_ValidMaxLength()
    {
        // Arrange: Create a witness with both scripts at max length
        var invocationScript = new byte[MaxInvocationScriptLength];
        var verificationScript = new byte[MaxVerificationScriptLength];
        var invocationBase64 = Convert.ToBase64String(invocationScript);
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = verificationBase64
            }
        };

        // Act
        var result = json.ToSignersAndWitnesses(_addressVersion);

        // Assert
        Assert.HasCount(1, result.Signers);
        Assert.HasCount(1, result.Witnesses);
        Assert.AreEqual(MaxInvocationScriptLength, result.Witnesses[0].InvocationScript.Length);
        Assert.AreEqual(MaxVerificationScriptLength, result.Witnesses[0].VerificationScript.Length);
    }

    [TestMethod]
    public void TestWitnessSize_BothScripts_InvocationExceedsMax()
    {
        // Arrange: Invocation exceeds, verification is valid
        var invocationScript = new byte[MaxInvocationScriptLength + 1];
        var verificationScript = new byte[MaxVerificationScriptLength];
        var invocationBase64 = Convert.ToBase64String(invocationScript);
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = verificationBase64
            }
        };

        // Act & Assert
        Assert.ThrowsExactly<RpcException>(
            () => json.ToSignersAndWitnesses(_addressVersion));
    }

    [TestMethod]
    public void TestWitnessSize_BothScripts_VerificationExceedsMax()
    {
        // Arrange: Verification exceeds, invocation is valid
        var invocationScript = new byte[MaxInvocationScriptLength];
        var verificationScript = new byte[MaxVerificationScriptLength + 1];
        var invocationBase64 = Convert.ToBase64String(invocationScript);
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = verificationBase64
            }
        };

        // Act & Assert
        Assert.ThrowsExactly<RpcException>(
            () => json.ToSignersAndWitnesses(_addressVersion));
    }

    [TestMethod]
    public void TestWitnessSize_MultipleWitnesses_AllValid()
    {
        // Arrange: Multiple witnesses with valid sizes
        var invocationScript = new byte[512];
        var verificationScript = new byte[512];
        var invocationBase64 = Convert.ToBase64String(invocationScript);
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = verificationBase64
            },
            new JObject
            {
                ["account"] = "0xabcdef1234567890abcdef1234567890abcdef12",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = verificationBase64
            }
        };

        // Act
        var result = json.ToSignersAndWitnesses(_addressVersion);

        // Assert
        Assert.HasCount(2, result.Signers);
        Assert.HasCount(2, result.Witnesses);
        Assert.AreEqual(512, result.Witnesses[0].InvocationScript.Length);
        Assert.AreEqual(512, result.Witnesses[0].VerificationScript.Length);
        Assert.AreEqual(512, result.Witnesses[1].InvocationScript.Length);
        Assert.AreEqual(512, result.Witnesses[1].VerificationScript.Length);
    }

    [TestMethod]
    public void TestWitnessSize_MultipleWitnesses_SecondInvalid()
    {
        // Arrange: Second witness has invalid invocation script size
        var validInvocationScript = new byte[512];
        var invalidInvocationScript = new byte[MaxInvocationScriptLength + 1];
        var verificationScript = new byte[512];
        var validInvocationBase64 = Convert.ToBase64String(validInvocationScript);
        var invalidInvocationBase64 = Convert.ToBase64String(invalidInvocationScript);
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = validInvocationBase64,
                ["verification"] = verificationBase64
            },
            new JObject
            {
                ["account"] = "0xabcdef1234567890abcdef1234567890abcdef12",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invalidInvocationBase64,
                ["verification"] = verificationBase64
            }
        };

        // Act & Assert
        Assert.ThrowsExactly<RpcException>(
            () => json.ToSignersAndWitnesses(_addressVersion));
    }

    [TestMethod]
    public void TestWitnessSize_EmptyScripts_Valid()
    {
        // Arrange: Both scripts are empty (valid case)
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = "",
                ["verification"] = ""
            }
        };

        // Act
        var result = json.ToSignersAndWitnesses(_addressVersion);

        // Assert
        Assert.HasCount(1, result.Signers);
        Assert.HasCount(1, result.Witnesses);
        Assert.AreEqual(0, result.Witnesses[0].InvocationScript.Length);
        Assert.AreEqual(0, result.Witnesses[0].VerificationScript.Length);
    }

    [TestMethod]
    public void TestWitnessSize_OneByteOverLimit_InvocationScript()
    {
        // Arrange: Test boundary condition - exactly 1 byte over limit
        var invocationScript = new byte[MaxInvocationScriptLength + 1];
        var invocationBase64 = Convert.ToBase64String(invocationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = invocationBase64,
                ["verification"] = ""
            }
        };

        // Act & Assert
        Assert.ThrowsExactly<RpcException>(
            () => json.ToSignersAndWitnesses(_addressVersion));
    }

    [TestMethod]
    public void TestWitnessSize_OneByteOverLimit_VerificationScript()
    {
        // Arrange: Test boundary condition - exactly 1 byte over limit
        var verificationScript = new byte[MaxVerificationScriptLength + 1];
        var verificationBase64 = Convert.ToBase64String(verificationScript);
        var json = new JArray
        {
            new JObject
            {
                ["account"] = "0x1234567890abcdef1234567890abcdef12345678",
                ["scopes"] = "CalledByEntry",
                ["invocation"] = "",
                ["verification"] = verificationBase64
            }
        };

        // Act & Assert
        Assert.ThrowsExactly<RpcException>(
            () => json.ToSignersAndWitnesses(_addressVersion));
    }
}
