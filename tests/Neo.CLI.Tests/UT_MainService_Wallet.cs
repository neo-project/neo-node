// Copyright (C) 2015-2025 The Neo Project.
//
// UT_MainService_Wallet.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Reflection;

namespace Neo.CLI.Tests;
[TestClass]
public class UT_MainService_Wallet
{
    private NeoSystem _neoSystem;

    [TestInitialize]
    public void TestSetup()
    {
        _neoSystem = TestBlockchain.GetSystem();
    }

    [TestMethod]
    public void TestOnSignMessageCommand()
    {
        var walletPassword = "test_pwd";
        var message = "this is a test to sign";

        var wallet = TestUtils.GenerateTestWallet(walletPassword);
        var account = wallet.CreateAccount();
        Assert.IsNotNull(account, "Wallet.CreateAccount() should create an account");

        var service = new MainService();

        TrySet(service, "NeoSystem", _neoSystem);
        TrySetField(service, "_neoSystem", _neoSystem);
        TrySet(service, "CurrentWallet", wallet);
        TrySetField(service, "_currentWallet", wallet);

        var readInputProp = service.GetType().GetProperty(
            "ReadUserInput",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsNotNull(readInputProp, "ReadUserInput property not found on MainService");

        Func<string, bool, string> fakeReadInput = (label, isPassword) =>
        {
            Assert.AreEqual("password", label);
            Assert.IsTrue(isPassword);
            return walletPassword;
        };

        readInputProp!.SetValue(service, fakeReadInput);

        var originalOut = Console.Out;
        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);

        try
        {
            InvokeNonPublic(service, "OnSignMessageCommand", message);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = outputWriter.ToString();
        // Basic headers
        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Output from sign message command should not be empty");
        Assert.Contains("Signed Payload", output, "Output should containt the signed payload");
        Assert.Contains("Algorithm", output, "Output should describe the algorithm used");
        Assert.Contains("Generated signatures", output, "Output should contain signatures header");
        // Sign block
        Assert.Contains("Address", output, "Output should contain at least one address");
        Assert.Contains("PublicKey", output, "Output should contain the public key");
        Assert.Contains("Signature", output, "Output should contain the signature");
        Assert.Contains("Salt", output, "Output should contain the salt used");
        // Check Salt
        var salt = ExtractHexValue(output, "Salt:");
        Assert.IsNotNull(salt, "Salt hex should be present in the output");
        Assert.AreEqual(32, salt!.Length, "Salt should be 16 bytes (32 hex chars)");
        Assert.IsTrue(IsHexString(salt!), "Salt should be valid hex");
    }
    private static string ExtractHexValue(string output, string label)
    {
        var index = output.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var start = index + label.Length;
        var endOfLine = output.IndexOfAny(new[] { '\r', '\n' }, start);
        if (endOfLine < 0)
            endOfLine = output.Length;

        var value = output[start..endOfLine].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
    private static bool IsHexString(string value)
    {
        foreach (var c in value)
        {
            var isHex =
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');

            if (!isHex) return false;
        }

        return true;
    }
    private static void TrySet(object target, string propertyName, object value)
    {
        var prop = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        prop?.SetValue(target, value);
    }
    private static void TrySetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        field?.SetValue(target, value);
    }
    private static void InvokeNonPublic(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        Assert.IsNotNull(method, $"Method '{methodName}' not found on type '{target.GetType().FullName}'.");
        method.Invoke(target, args);
    }
}
