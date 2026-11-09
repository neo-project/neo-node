// Copyright (C) 2015-2026 The Neo Project.
//
// UT_MainService_Wallet.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.Providers;
using Neo.Sign;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.BIP32;
using Neo.Wallets.NEP6;
using System.Reflection;
using System.Text;

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
        var output = CreateWalletAngSignMessage(walletPassword, true);

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
    [TestMethod]
    public void TestOnSignMessageCommandWithoutPassword()
    {
        var output = CreateWalletAngSignMessage(string.Empty, true);

        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Output should not be empty");
        Assert.Contains("Cancelled", output, "Output should contain cancellation message");
    }
    [TestMethod]
    public void TestOnSignMessageCommandWrongPassword()
    {
        var walletPassword = "invalid_pwd";
        var output = CreateWalletAngSignMessage(walletPassword, true);

        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Output should not be empty");
        Assert.Contains("Incorrect password", output, "Output should contain incorrect password");
        Assert.DoesNotContain("Signed Payload", output, "Output should not containt signed payload");
        Assert.DoesNotContain("Generated signatures", output, "Output should not containt signatures");
    }
    [TestMethod]
    public void TestOnSignMessageCommandWithoutAccount()
    {
        var walletPassword = "test_pwd";
        var output = CreateWalletAngSignMessage(walletPassword, true, false);

        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Output should not be empty");
        Assert.Contains("Signed Payload", output, "Output should containt signed payload");
        Assert.Contains("Generated signatures", output, "Output should containt signatures");
        Assert.DoesNotContain("Address:", output, "Output should not containt Address");
        Assert.DoesNotContain("PublicKey:", output, "Output should not containt PublicKey");
        Assert.DoesNotContain("Signature:", output, "Output should not containt Signature");
        Assert.DoesNotContain("Salt:", output, "Output should not containt Salt");
    }
    [TestMethod]
    public void TestOnSignMessageCommandWithNullMessage()
    {
        var walletPassword = "test_pwd";
        var output = CreateWalletAngSignMessage(walletPassword, true, true, null);
        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Output should not be empty");
        Assert.Contains("Null message", output, "Output should contain null message");
    }
    [TestMethod]
    public void TestOnSignMessageCommandWithQuotes()
    {
        var walletPassword = "test_pwd";
        string message = "this is a test to sign";
        var outputWithoutQuotes = CreateWalletAngSignMessage(walletPassword, true, true, message);
        var outputWithDoubleQuotes = CreateWalletAngSignMessage(walletPassword, true, true, $"\"{message}\"");
        var outputWithSingleQuotes = CreateWalletAngSignMessage(walletPassword, true, true, $"'{message}'");

        Assert.IsFalse(string.IsNullOrWhiteSpace(outputWithoutQuotes), "Output without quotes should not be empty");
        Assert.IsFalse(string.IsNullOrWhiteSpace(outputWithDoubleQuotes), "Output with double quotes should not be empty");
        Assert.IsFalse(string.IsNullOrWhiteSpace(outputWithSingleQuotes), "Output with single quotes should not be empty");

        var payloadWithoutQuotes = ExtractHexValue(outputWithoutQuotes, "Signed Payload:");
        var payloadWithDoubleQuotes = ExtractHexValue(outputWithDoubleQuotes, "Signed Payload:");
        var payloadWithSingleQuotes = ExtractHexValue(outputWithSingleQuotes, "Signed Payload:");

        Assert.IsNotNull(payloadWithoutQuotes, "Signed payload should be present when signing without quotes");
        Assert.IsNotNull(payloadWithDoubleQuotes, "Signed payload should be present when signing with double quotes");
        Assert.IsNotNull(payloadWithSingleQuotes, "Signed payload should be present when signing with single quotes");

        var msgPayloadWithoutQuotes = ExtractMessageFromSignedPayload(payloadWithoutQuotes);
        var msgPayloadWithDoubleQuotes = ExtractMessageFromSignedPayload(payloadWithDoubleQuotes);
        var msgPayloadWithSingleQuotes = ExtractMessageFromSignedPayload(payloadWithSingleQuotes);

        Assert.AreEqual(msgPayloadWithoutQuotes, msgPayloadWithDoubleQuotes, "Signing a message with surrounding double quotes should produce the same normalized message as a signing without quotes");
        Assert.AreEqual(msgPayloadWithoutQuotes, msgPayloadWithSingleQuotes, "Signing a message with surrounding single quotes should produce the same normalized message as a signing without quotes");
    }

    [TestMethod]
    public void TestOnCommandSignMessagePrefersLongestCommand()
    {
        var output = CreateWalletAndRunCommand("test_pwd", "sign message \"wdadhauoi$#@$#@\" false");

        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Output should not be empty");
        Assert.Contains("Signed Payload", output, "Output should contain signed payload");
        Assert.IsFalse(output.Contains("invalid start of a value", StringComparison.OrdinalIgnoreCase),
            "Running 'sign message' should not emit the shorter 'sign' command JSON parse error.");
    }

    [TestMethod]
    public void TestOnSignCommandStillWorks()
    {
        var output = CreateWalletAndRunSignCommand();

        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Output should not be empty");
        Assert.Contains("Signed Output", output, "The sign command should still sign context JSON successfully.");
    }

    [TestMethod]
    public void TestOnVerifyMessageCommand()
    {
        var walletPassword = "test_pwd";
        var message = "this is a test to sign";

        // 1) First sign a message to obtain (signature, pubkey, salt, address)
        var signOutput = CreateWalletAngSignMessage(walletPassword, true, withAccount: true, messageToSign: message);

        var signature = ExtractHexValue(signOutput, "Signature:");
        var publicKey = ExtractHexValue(signOutput, "PublicKey:");
        var salt = ExtractHexValue(signOutput, "Salt:");
        var address = ExtractHexValue(signOutput, "Address:");

        Assert.IsFalse(string.IsNullOrWhiteSpace(signOutput), "Sign output should not be empty");
        Assert.IsNotNull(signature, "Signature should be present in sign output");
        Assert.IsNotNull(publicKey, "PublicKey should be present in sign output");
        Assert.IsNotNull(salt, "Salt should be present in sign output");
        Assert.IsNotNull(address, "Address should be present in sign output");

        Assert.IsTrue(IsHexString(signature!), "Signature should be valid hex");
        Assert.AreEqual(0, signature!.Length % 2, "Signature hex should have even length");

        Assert.IsTrue(IsHexString(publicKey!), "PublicKey should be valid hex");
        Assert.AreEqual(0, publicKey!.Length % 2, "PublicKey hex should have even length");

        Assert.IsTrue(IsHexString(salt!), "Salt should be valid hex");
        Assert.AreEqual(32, salt!.Length, "Salt should be 16 bytes (32 hex chars)");

        // 2) Verify with the exact message
        var verifyOutput = CreateServiceAndVerifyMessage(message, signature!, publicKey!, salt!, true);
        Assert.IsFalse(string.IsNullOrWhiteSpace(verifyOutput), "Verify output should not be empty");
        Assert.Contains("Verification Result", verifyOutput, "Should print verification header");
        Assert.Contains("Status:", verifyOutput, "Should print Status line");
        Assert.Contains("Valid", verifyOutput, "Signature should be valid");
        Assert.Contains(address!, verifyOutput, "Address should match the signer");

        // 3) Message normalization: verify with surrounding quotes should still be valid
        var verifyOutputQuoted = CreateServiceAndVerifyMessage($"\"{message}\"", signature!, publicKey!, salt!, true);
        Assert.Contains("Status:", verifyOutputQuoted, "Should print Status line for quoted message");
        Assert.Contains("Valid", verifyOutputQuoted, "Quoted message should still verify as valid");

        // 4) Tampered message: should be invalid and print debug info
        var verifyOutputInvalid = CreateServiceAndVerifyMessage(message + "!", signature!, publicKey!, salt!, true);
        Assert.Contains("Status:", verifyOutputInvalid, "Should print Status line for invalid message");
        Assert.Contains("Invalid", verifyOutputInvalid, "Tampered message should be invalid");
        Assert.Contains("Debug Information", verifyOutputInvalid, "Should print debug information on invalid signature");
        Assert.Contains("Reconstructed Payload", verifyOutputInvalid, "Should print reconstructed payload in debug mode");

        // 5) Input validation: invalid public key format
        var invalidPubKeyOutput = CreateServiceAndVerifyMessage(message, signature!, "zzzz", salt!, true);
        Assert.Contains("Invalid public key format", invalidPubKeyOutput, "Should reject invalid public key");

        // 6) Input validation: invalid signature hex
        var invalidSignatureOutput = CreateServiceAndVerifyMessage(message, "NOT_HEX", publicKey!, salt!, true);
        Assert.Contains("Invalid signature format", invalidSignatureOutput, "Should reject invalid signature format");

        // 7) Input validation: empty salt
        var emptySaltOutput = CreateServiceAndVerifyMessage(message, signature!, publicKey!, string.Empty, true);
        Assert.Contains("Salt cannot be empty", emptySaltOutput, "Should reject empty salt");
    }

    [TestMethod]
    public void TestOnSignWithSignatureReplayOnVerifyWithoutSignatureReplay()
    {
        var walletPassword = "test_pwd";
        var message = "this is a test to sign";

        // 1) First sign a message to obtain with signature replay (signature, pubkey, salt, address)
        var signOutputWithSR = CreateWalletAngSignMessage(walletPassword, true, withAccount: true, messageToSign: message);

        var signatureSR = ExtractHexValue(signOutputWithSR, "Signature:");
        var publicKeySR = ExtractHexValue(signOutputWithSR, "PublicKey:");
        var saltSR = ExtractHexValue(signOutputWithSR, "Salt:");
        var addressSR = ExtractHexValue(signOutputWithSR, "Address:");

        // 2) Verify with the exact message with signature replay
        var verifyOutput = CreateServiceAndVerifyMessage(message, signatureSR!, publicKeySR!, saltSR!, false);
        Assert.IsFalse(string.IsNullOrWhiteSpace(verifyOutput), "Verify output should not be empty");
        Assert.Contains("Verification Result", verifyOutput, "Should print verification header");
        Assert.Contains("Status:", verifyOutput, "Should print Status line");
        Assert.Contains("Invalid", verifyOutput, "Signature should be valid");
        Assert.Contains(addressSR!, verifyOutput, "Address should match the signer");

        // 3) First sign a message to obtain without signature replay (signature, pubkey, salt, address)
        var signOutputWithoutSR = CreateWalletAngSignMessage(walletPassword, false, withAccount: true, messageToSign: message);

        var signatureWithoutSR = ExtractHexValue(signOutputWithoutSR, "Signature:");
        var publicKeyWithoutSR = ExtractHexValue(signOutputWithoutSR, "PublicKey:");
        var saltWithoutSR = ExtractHexValue(signOutputWithoutSR, "Salt:");
        var addressWithoutSR = ExtractHexValue(signOutputWithoutSR, "Address:");

        // 4) Verify with the exact message without signature replay
        var verifyWithoutOutput = CreateServiceAndVerifyMessage(message, signatureWithoutSR!, publicKeyWithoutSR!, saltWithoutSR!, true);
        Assert.IsFalse(string.IsNullOrWhiteSpace(verifyWithoutOutput), "Verify output should not be empty");
        Assert.Contains("Verification Result", verifyWithoutOutput, "Should print verification header");
        Assert.Contains("Status:", verifyWithoutOutput, "Should print Status line");
        Assert.Contains("Invalid", verifyWithoutOutput, "Signature should be valid");
        Assert.Contains(addressWithoutSR!, verifyWithoutOutput, "Address should match the signer");
    }

    [TestMethod]
    public void TestOnSignOnVerifyMessageWithoutSignatureReplay()
    {
        var walletPassword = "test_pwd";
        var message = "this is a test to sign";

        // 1) First sign a message to obtain (signature, pubkey, salt, address)
        var signOutput = CreateWalletAngSignMessage(walletPassword, false, withAccount: true, messageToSign: message);

        var signature = ExtractHexValue(signOutput, "Signature:");
        var publicKey = ExtractHexValue(signOutput, "PublicKey:");
        var salt = ExtractHexValue(signOutput, "Salt:");
        var address = ExtractHexValue(signOutput, "Address:");

        Assert.IsFalse(string.IsNullOrWhiteSpace(signOutput), "Sign output should not be empty");
        Assert.IsNotNull(signature, "Signature should be present in sign output");
        Assert.IsNotNull(publicKey, "PublicKey should be present in sign output");
        Assert.IsNotNull(salt, "Salt should be present in sign output");
        Assert.IsNotNull(address, "Address should be present in sign output");

        Assert.IsTrue(IsHexString(signature!), "Signature should be valid hex");
        Assert.AreEqual(0, signature!.Length % 2, "Signature hex should have even length");

        Assert.IsTrue(IsHexString(publicKey!), "PublicKey should be valid hex");
        Assert.AreEqual(0, publicKey!.Length % 2, "PublicKey hex should have even length");

        Assert.IsTrue(IsHexString(salt!), "Salt should be valid hex");
        Assert.AreEqual(32, salt!.Length, "Salt should be 16 bytes (32 hex chars)");

        // 2) Verify with the exact message
        var verifyOutput = CreateServiceAndVerifyMessage(message, signature!, publicKey!, salt!, false);
        Assert.IsFalse(string.IsNullOrWhiteSpace(verifyOutput), "Verify output should not be empty");
        Assert.Contains("Verification Result", verifyOutput, "Should print verification header");
        Assert.Contains("Status:", verifyOutput, "Should print Status line");
        Assert.Contains("Valid", verifyOutput, "Signature should be valid");
        Assert.Contains(address!, verifyOutput, "Address should match the signer");

        // 3) Message normalization: verify with surrounding quotes should still be valid
        var verifyOutputQuoted = CreateServiceAndVerifyMessage($"\"{message}\"", signature!, publicKey!, salt!, false);
        Assert.Contains("Status:", verifyOutputQuoted, "Should print Status line for quoted message");
        Assert.Contains("Valid", verifyOutputQuoted, "Quoted message should still verify as valid");

        // 4) Tampered message: should be invalid and print debug info
        var verifyOutputInvalid = CreateServiceAndVerifyMessage(message + "!", signature!, publicKey!, salt!, false);
        Assert.Contains("Status:", verifyOutputInvalid, "Should print Status line for invalid message");
        Assert.Contains("Invalid", verifyOutputInvalid, "Tampered message should be invalid");
        Assert.Contains("Debug Information", verifyOutputInvalid, "Should print debug information on invalid signature");
        Assert.Contains("Reconstructed Payload", verifyOutputInvalid, "Should print reconstructed payload in debug mode");

        // 5) Input validation: invalid public key format
        var invalidPubKeyOutput = CreateServiceAndVerifyMessage(message, signature!, "zzzz", salt!, false);
        Assert.Contains("Invalid public key format", invalidPubKeyOutput, "Should reject invalid public key");

        // 6) Input validation: invalid signature hex
        var invalidSignatureOutput = CreateServiceAndVerifyMessage(message, "NOT_HEX", publicKey!, salt!, false);
        Assert.Contains("Invalid signature format", invalidSignatureOutput, "Should reject invalid signature format");

        // 7) Input validation: empty salt
        var emptySaltOutput = CreateServiceAndVerifyMessage(message, signature!, publicKey!, string.Empty, false);
        Assert.Contains("Salt cannot be empty", emptySaltOutput, "Should reject empty salt");
    }

    [TestMethod]
    public void TestStartAppliesConfiguredLogDirectory()
    {
        var previousSettings = Settings.Custom;
        var logDirectory = Path.Combine(Path.GetTempPath(), $"neo-cli-logs-{Guid.NewGuid():N}");

        try
        {
            Settings.Custom = new Settings
            {
                Logger = new LoggerSettings { Active = true, Path = logDirectory, ConsoleOutput = false },
                Storage = new StorageSettings { Engine = nameof(MemoryStore), Path = string.Empty },
                P2P = new P2PSettings(),
                UnlockWallet = new UnlockWalletSettings(),
                Contracts = new ContractsSettings(),
                Plugins = new PluginsSettings()
            };

            var service = new MainService();
            service.Start(new CommandLineOptions());

            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                var neoSystemField = typeof(MainService).GetField("_neoSystem", BindingFlags.NonPublic | BindingFlags.Instance);
                return neoSystemField?.GetValue(service) is not null;
            }, TimeSpan.FromSeconds(5)));
            service.Stop();
        }
        finally
        {
            Settings.Custom = previousSettings;
        }
    }

    [TestMethod]
    public void ImportKey_MissingFile_WritesError()
    {
        var wallet = CreateTempWallet();
        var missing = Path.Combine(Path.GetTempPath(), $"missing-keys-{Guid.NewGuid():N}.txt");
        try
        {
            var output = RunImportKey(wallet, missing, (_, _) => throw new InvalidOperationException("prompt should not run"));

            Assert.Contains("doesn't exists", output);
            Assert.IsEmpty(wallet.GetAccounts().ToList());
        }
        finally
        {
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void ImportKey_SmallFile_DoesNotPromptAndImports()
    {
        var wallet = CreateTempWallet();
        var source = CreateTempWallet();
        var wif = source.CreateAccount()!.GetKey()!.Export();
        var path = Path.Combine(Path.GetTempPath(), $"keys-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, wif + Environment.NewLine);
            Assert.IsLessThan(1024 * 1024, new FileInfo(path).Length);

            var prompted = false;
            var output = RunImportKey(wallet, path, (_, _) =>
            {
                prompted = true;
                return "no";
            });

            Assert.IsFalse(prompted, "A short key file must not use the too-big prompt (path length is not the file size).");
            Assert.HasCount(1, wallet.GetAccounts().ToList());
            Assert.DoesNotContain("doesn't exists", output);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void ImportKey_LargeFileDeclined_DoesNotImport()
    {
        var wallet = CreateTempWallet();
        var source = CreateTempWallet();
        var wif = source.CreateAccount()!.GetKey()!.Export();
        var path = Path.Combine(Path.GetTempPath(), $"keys-{Guid.NewGuid():N}.txt");
        try
        {
            WriteOversizedKeyFile(path, wif);
            Assert.IsGreaterThan(1024 * 1024, new FileInfo(path).Length);
            Assert.IsLessThan(1024 * 1024, path.Length);

            var output = RunImportKey(wallet, path, (prompt, _) =>
            {
                Assert.Contains("too big", prompt);
                return "no";
            });

            Assert.IsEmpty(wallet.GetAccounts().ToList());
            Assert.DoesNotContain("doesn't exists", output);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void ImportKey_LargeFileAccepted_Imports()
    {
        var wallet = CreateTempWallet();
        var source = CreateTempWallet();
        var wif = source.CreateAccount()!.GetKey()!.Export();
        var path = Path.Combine(Path.GetTempPath(), $"keys-{Guid.NewGuid():N}.txt");
        try
        {
            WriteOversizedKeyFile(path, wif);

            RunImportKey(wallet, path, (prompt, _) =>
            {
                Assert.Contains("too big", prompt);
                return "yes";
            });

            Assert.HasCount(1, wallet.GetAccounts().ToList());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void ImportMnemonic_SharedEnglishWords_ImportsDerivedAccount()
    {
        const string phrase = "bonus caution capable label digital village surprise image figure sentence exact capable";
        var wallet = CreateTempWallet();
        try
        {
            var output = RunImportMnemonic(wallet, phrase, "", 0, out bool prompted);

            Assert.IsTrue(prompted);
            AssertImportedMnemonic(wallet, output, phrase, "", 0);
        }
        finally
        {
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void ImportMnemonic_PassphraseAndIndex_ChangeDerivedAccount()
    {
        const string phrase = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        var emptyWallet = CreateTempWallet();
        var passphraseWallet = CreateTempWallet();
        try
        {
            RunImportMnemonic(emptyWallet, phrase, "", 0, out _);
            RunImportMnemonic(emptyWallet, phrase, "", 1, out _);
            RunImportMnemonic(passphraseWallet, phrase, "TREZOR", 0, out _);

            var accounts = emptyWallet.GetAccounts().ToList();
            Assert.HasCount(2, accounts);
            Assert.IsTrue(accounts[0].GetKey()!.PublicKey.Equals(KeyFromMnemonic(phrase, "", 0).PublicKey) ||
                accounts[1].GetKey()!.PublicKey.Equals(KeyFromMnemonic(phrase, "", 0).PublicKey));
            Assert.IsTrue(accounts.Any(account => account.GetKey()!.PublicKey.Equals(KeyFromMnemonic(phrase, "", 1).PublicKey)));
            Assert.IsFalse(accounts.Any(account => account.GetKey()!.PublicKey.Equals(KeyFromMnemonic(phrase, "TREZOR", 0).PublicKey)));
            Assert.IsTrue(passphraseWallet.GetAccounts().Single().GetKey()!.PublicKey.Equals(KeyFromMnemonic(phrase, "TREZOR", 0).PublicKey));
        }
        finally
        {
            TryDelete(emptyWallet.Path);
            TryDelete(passphraseWallet.Path);
        }
    }

    [TestMethod]
    public void ImportMnemonic_InvalidPhrase_DoesNotImport()
    {
        var wallet = CreateTempWallet();
        try
        {
            var output = RunImportMnemonic(
                wallet,
                "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon",
                "",
                0,
                out bool prompted);

            Assert.IsFalse(prompted);
            Assert.Contains("Error", output);
            Assert.IsEmpty(wallet.GetAccounts().ToList());
        }
        finally
        {
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void ImportMnemonic_WithoutWallet_WritesError()
    {
        var service = new MainService();
        var output = CaptureConsole(() =>
            TestUtils.InvokeNonPublic(service, "OnImportMnemonicCommand", 0u));

        Assert.Contains("open the wallet first", output);
    }

    [TestMethod]
    public void ImportMnemonic_EmptyPhrase_Cancels()
    {
        var wallet = CreateTempWallet();
        try
        {
            var output = RunImportMnemonic(wallet, "", "", 0, out bool prompted);

            Assert.IsFalse(prompted);
            Assert.Contains("Cancelled", output);
            Assert.IsEmpty(wallet.GetAccounts().ToList());
        }
        finally
        {
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void ImportMnemonic_IndexOutOfRange_WritesError()
    {
        var wallet = CreateTempWallet();
        try
        {
            var output = RunImportMnemonic(wallet, "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", "", 0x80000000, out bool prompted);

            Assert.IsFalse(prompted);
            Assert.Contains("Address index", output);
            Assert.IsEmpty(wallet.GetAccounts().ToList());
        }
        finally
        {
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void CreateAddress_ShowsMnemonicOnceAndDoesNotWriteItToFile()
    {
        var wallet = CreateTempWallet();
        var addressFile = Path.Combine(Directory.GetCurrentDirectory(), "address.txt");
        if (File.Exists(addressFile))
            File.Delete(addressFile);

        var service = CreateWalletService(wallet);
        try
        {
            var output = CaptureConsole(() => TestUtils.InvokeNonPublic(service, "OnCreateAddressCommand", (ushort)2));

            Assert.Contains("only be shown once", output);
            var phrases = ExtractLabeledLines(output, "Mnemonic:");
            var addresses = ExtractLabeledLines(output, "Address:");
            Assert.HasCount(2, phrases);
            Assert.HasCount(2, addresses);
            Assert.AreEqual(12, phrases[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.AreNotEqual(phrases[0], phrases[1]);

            var accounts = wallet.GetAccounts().ToList();
            Assert.HasCount(2, accounts);
            foreach (string phrase in phrases)
            {
                var expected = KeyFromMnemonic(phrase, "", 0);
                Assert.IsTrue(accounts.Any(account => account.GetKey()!.PublicKey.Equals(expected.PublicKey)));
            }

            string exported = File.ReadAllText(addressFile);
            foreach (string phrase in phrases)
                Assert.DoesNotContain(phrase, exported);
            foreach (string address in addresses)
                Assert.Contains(address, exported);
            Assert.DoesNotContain(phrases[0], File.ReadAllText(wallet.Path));
        }
        finally
        {
            if (File.Exists(addressFile))
                File.Delete(addressFile);
            TryDelete(wallet.Path);
        }
    }

    [TestMethod]
    public void CreateWallet_ShowsMnemonicOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), $"neo-cli-wallet-{Guid.NewGuid():N}.json");
        var service = new MainService();
        TestUtils.TrySet(service, "NeoSystem", _neoSystem);
        TestUtils.TrySetField(service, "_neoSystem", _neoSystem);
        try
        {
            var output = CaptureConsole(() => service.CreateWallet(path, "test_pwd", true, "mnemonic-wallet"));

            Assert.Contains("only be shown once", output);
            string phrase = ExtractLabeledLines(output, "Mnemonic:").Single();
            Assert.AreEqual(12, phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Contains("m/44'/888'/0'/0/0", output);

            var account = service.CurrentWallet!.GetAccounts().Single();
            Assert.IsTrue(account.GetKey()!.PublicKey.Equals(KeyFromMnemonic(phrase, "", 0).PublicKey));
            Assert.DoesNotContain(phrase, File.ReadAllText(path));
        }
        finally
        {
            if (service.CurrentWallet is not null)
                SignerManager.UnregisterSigner(service.CurrentWallet.Name);
            TryDelete(path);
        }
    }

    private string RunImportKey(NEP6Wallet wallet, string wifOrFile, Func<string, bool, string> readInput)
    {
        var service = new MainService();
        TestUtils.TrySet(service, "NeoSystem", _neoSystem);
        TestUtils.TrySetField(service, "_neoSystem", _neoSystem);
        TestUtils.TrySet(service, "CurrentWallet", wallet);
        TestUtils.TrySetField(service, "_currentWallet", wallet);
        var readInputProp = service.GetType().GetProperty(
            "ReadUserInput",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(readInputProp, "ReadUserInput property not found on MainService");
        readInputProp!.SetValue(service, readInput);

        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);
        Console.SetError(outputWriter);
        try
        {
            TestUtils.InvokeNonPublic(service, "OnImportKeyCommand", wifOrFile);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return outputWriter.ToString();
    }

    private string RunImportMnemonic(NEP6Wallet wallet, string mnemonic, string passphrase, uint index, out bool prompted)
    {
        var service = CreateWalletService(wallet);
        var wasPrompted = false;
        var readInputProp = service.GetType().GetProperty(
            "ReadUserInput",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(readInputProp, "ReadUserInput property not found on MainService");
        readInputProp!.SetValue(service, (Func<string, bool, string>)((prompt, hidden) =>
        {
            Assert.IsTrue(hidden);
            if (prompt.Contains("mnemonic", StringComparison.OrdinalIgnoreCase))
                return mnemonic;

            wasPrompted = true;
            Assert.Contains("passphrase", prompt);
            return passphrase;
        }));

        var output = CaptureConsole(() => TestUtils.InvokeNonPublic(service, "OnImportMnemonicCommand", index));
        prompted = wasPrompted;
        return output;
    }

    private static void AssertImportedMnemonic(NEP6Wallet wallet, string output, string phrase, string passphrase, uint index)
    {
        var expected = KeyFromMnemonic(phrase, passphrase, index);
        var account = wallet.GetAccounts().Single();
        Assert.IsTrue(account.GetKey()!.PublicKey.Equals(expected.PublicKey));
        Assert.Contains(account.Address, output);
        Assert.Contains($"m/44'/888'/0'/0/{index}", output);
        Assert.DoesNotContain(phrase, output);
        Assert.DoesNotContain(phrase, File.ReadAllText(wallet.Path));
    }

    private static MainService CreateWalletService(NEP6Wallet wallet)
    {
        var service = new MainService();
        var system = TestBlockchain.GetSystem();
        TestUtils.TrySet(service, "NeoSystem", system);
        TestUtils.TrySetField(service, "_neoSystem", system);
        TestUtils.TrySet(service, "CurrentWallet", wallet);
        TestUtils.TrySetField(service, "_currentWallet", wallet);
        return service;
    }

    private static string CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);
        Console.SetError(outputWriter);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return outputWriter.ToString();
    }

    private static List<string> ExtractLabeledLines(string output, string label)
    {
        var values = new List<string>();
        foreach (string raw in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int index = raw.IndexOf(label, StringComparison.Ordinal);
            if (index < 0)
                continue;
            values.Add(raw[(index + label.Length)..].Trim());
        }

        return values;
    }

    private static KeyPair KeyFromMnemonic(string phrase, string passphrase, uint index)
    {
        byte[] seed = Mnemonic.Parse(phrase).DeriveSeed(passphrase);
        try
        {
            ExtendedKey key = ExtendedKey.Create(seed, $"m/44'/888'/0'/0/{index}");
            return new KeyPair((byte[])key.PrivateKey.Clone());
        }
        finally
        {
            Array.Clear(seed, 0, seed.Length);
        }
    }

    private static NEP6Wallet CreateTempWallet()
    {
        var path = Path.Combine(Path.GetTempPath(), $"neo-cli-wallet-{Guid.NewGuid():N}.json");
        var json = new JObject()
        {
            ["name"] = "noname",
            ["version"] = new Version("1.0").ToString(),
            ["scrypt"] = new ScryptParameters(2, 1, 1).ToJson(),
            ["accounts"] = new JArray(),
            ["extra"] = null
        };
        return new NEP6Wallet(path, "test_pwd", TestProtocolSettings.Default, json);
    }

    private static void WriteOversizedKeyFile(string path, string wif)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine(wif);
        writer.Write(new string('\n', 1024 * 1024));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private string CreateServiceAndVerifyMessage(string message, string signature, string publicKey, string salt, bool avoidSignatureReplay)
    {
        var service = new MainService();

        TestUtils.TrySet(service, "NeoSystem", _neoSystem);
        TestUtils.TrySetField(service, "_neoSystem", _neoSystem);

        var originalOut = Console.Out;
        var originalErr = Console.Error;

        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);
        Console.SetError(outputWriter);

        try
        {
            TestUtils.InvokeNonPublic(service, "OnVerifyMessageCommand", message, signature, publicKey, salt, avoidSignatureReplay);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return outputWriter.ToString();
    }

    private string CreateWalletAngSignMessage(string userPassword, bool addSignData, bool withAccount = true, string messageToSign = "this is a test to sign")
    {
        var walletPassword = "test_pwd";
        var message = messageToSign;

        var wallet = TestUtils.GenerateTestWallet(walletPassword);
        if (withAccount)
        {
            var account = wallet.CreateAccount();
            Assert.IsNotNull(account, "Wallet.CreateAccount() should create an account");
        }

        var service = new MainService();

        TestUtils.TrySet(service, "NeoSystem", _neoSystem);
        TestUtils.TrySetField(service, "_neoSystem", _neoSystem);
        TestUtils.TrySet(service, "CurrentWallet", wallet);
        TestUtils.TrySetField(service, "_currentWallet", wallet);

        var readInputProp = service.GetType().GetProperty(
            "ReadUserInput",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsNotNull(readInputProp, "ReadUserInput property not found on MainService");

        Func<string, bool, string> fakeReadInput = (label, isPassword) =>
        {
            Assert.AreEqual("password", label);
            Assert.IsTrue(isPassword);
            return userPassword;
        };

        readInputProp!.SetValue(service, fakeReadInput);

        var originalOut = Console.Out;
        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);

        try
        {
            TestUtils.InvokeNonPublic(service, "OnSignMessageCommand", message, addSignData);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return outputWriter.ToString();
    }

    private string CreateWalletAndRunCommand(string userPassword, string command, bool withAccount = true)
    {
        var walletPassword = "test_pwd";
        var wallet = TestUtils.GenerateTestWallet(walletPassword);
        if (withAccount)
        {
            var account = wallet.CreateAccount();
            Assert.IsNotNull(account, "Wallet.CreateAccount() should create an account");
        }

        var service = new MainService();

        TestUtils.TrySet(service, "NeoSystem", _neoSystem);
        TestUtils.TrySetField(service, "_neoSystem", _neoSystem);
        TestUtils.TrySet(service, "CurrentWallet", wallet);
        TestUtils.TrySetField(service, "_currentWallet", wallet);

        var readInputProp = service.GetType().GetProperty(
            "ReadUserInput",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsNotNull(readInputProp, "ReadUserInput property not found on MainService");
        readInputProp!.SetValue(service, (Func<string, bool, string>)((label, isPassword) =>
        {
            Assert.AreEqual("password", label);
            Assert.IsTrue(isPassword);
            return userPassword;
        }));

        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);
        Console.SetError(outputWriter);

        try
        {
            var result = (bool)TestUtils.InvokeNonPublic(service, "OnCommand", command)!;
            Assert.IsTrue(result, "Command should be handled");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return outputWriter.ToString();
    }

    private string CreateWalletAndRunSignCommand()
    {
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        Assert.IsNotNull(account, "Wallet.CreateAccount() should create an account");

        var snapshot = _neoSystem.StoreView;
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 1,
            ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + _neoSystem.Settings.MaxValidUntilBlockIncrement,
            Signers = [new Signer { Account = account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = []
        };

        var context = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        var jsonObjectToSign = (JObject)JToken.Parse(context.ToString());

        var service = new MainService();

        TestUtils.TrySet(service, "NeoSystem", _neoSystem);
        TestUtils.TrySetField(service, "_neoSystem", _neoSystem);
        TestUtils.TrySet(service, "CurrentWallet", wallet);
        TestUtils.TrySetField(service, "_currentWallet", wallet);

        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);
        Console.SetError(outputWriter);

        try
        {
            TestUtils.InvokeNonPublic(service, "OnSignCommand", jsonObjectToSign);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return outputWriter.ToString();
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

    private static string ExtractMessageFromSignedPayload(string payloadHex)
    {
        var payloadBytes = HexToBytes(payloadHex);
        Assert.IsNotNull(payloadBytes);
        Assert.IsGreaterThan(4 + 1 + 32 + 2, payloadBytes.Length, "Payload is too short");

        Assert.AreEqual(0x01, payloadBytes[0], "Invalid payload prefix byte 0");
        Assert.AreEqual(0x00, payloadBytes[1], "Invalid payload prefix byte 1");
        Assert.AreEqual(0x01, payloadBytes[2], "Invalid payload prefix byte 2");
        Assert.AreEqual(0xF0, payloadBytes[3], "Invalid payload prefix byte 3");

        byte paramLength = payloadBytes[4];
        int paramStart = 5;

        Assert.IsGreaterThanOrEqualTo(paramStart + paramLength + 2, payloadBytes.Length, "Payload does not contain full param bytes");

        var paramBytes = new byte[paramLength];
        Buffer.BlockCopy(payloadBytes, paramStart, paramBytes, 0, paramLength);

        int suffixIndex = paramStart + paramLength;
        Assert.AreEqual(0x00, payloadBytes[suffixIndex], "Invalid payload suffix byte 0");
        Assert.AreEqual(0x00, payloadBytes[suffixIndex + 1], "Invalid payload suffix byte 1");

        var saltAndMessage = Encoding.UTF8.GetString(paramBytes);

        Assert.IsGreaterThanOrEqualTo(32, saltAndMessage.Length, "Salt+message data is too short");
        var message = saltAndMessage[32..];

        return message;
    }

    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have an even length", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
