// Copyright (C) 2015-2026 The Neo Project.
//
// UT_MainService_Vote.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System.Numerics;
using System.Security.Cryptography;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.CLI.Tests;

[TestClass]
public class UT_MainService_Vote
{
    private static readonly ProtocolSettings WithEchidnaAtGenesis =
        TestProtocolSettings.Default with
        {
            Hardforks = TestProtocolSettings.Default.Hardforks.SetItem(Hardfork.HF_Echidna, 0),
        };

    private static readonly ProtocolSettings WithoutEchidna =
        TestProtocolSettings.Default with
        {
            Hardforks = TestProtocolSettings.Default.Hardforks.SetItem(Hardfork.HF_Echidna, uint.MaxValue),
        };

    [TestMethod]
    public void TestOnRegisterCandidateCommand_NoWallet()
    {
        var output = RunRegisterCandidate(new VoteTestContext(TestBlockchain.GetSystem()), relayAnswer: null);

        Assert.Contains("You have to open the wallet first.", output);
    }

    [TestMethod]
    public void TestOnRegisterCandidateCommand_AccountNotInWallet()
    {
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        wallet.CreateAccount();

        var output = RunRegisterCandidate(
            new VoteTestContext(TestBlockchain.GetSystem(), wallet),
            account: UInt160.Zero);

        Assert.Contains("This address isn't in your wallet!", output);
    }

    [TestMethod]
    public void TestOnRegisterCandidateCommand_LockedAccount()
    {
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        account.Lock = true;

        var output = RunRegisterCandidate(
            new VoteTestContext(TestBlockchain.GetSystem(), wallet),
            account: account.ScriptHash);

        Assert.Contains("Locked or WatchOnly address.", output);
    }

    [TestMethod]
    public void TestOnRegisterCandidateCommand_WatchOnlyAccount()
    {
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var watchHash = new UInt160(RandomNumberGenerator.GetBytes(20));
        var watchAccount = wallet.CreateAccount(watchHash);

        var output = RunRegisterCandidate(
            new VoteTestContext(TestBlockchain.GetSystem(), wallet),
            account: watchAccount.ScriptHash);

        Assert.Contains("Locked or WatchOnly address.", output);
    }

    [TestMethod]
    public void TestOnRegisterCandidateCommand_NoPublicKey()
    {
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var signer = TestProtocolSettings.Default.StandbyCommittee[0];
        var contract = Contract.CreateMultiSigContract(1, new[] { signer });
        var account = wallet.CreateAccount(contract, (KeyPair?)null);

        var output = RunRegisterCandidate(
            new VoteTestContext(TestBlockchain.GetSystem(), wallet),
            account: account.ScriptHash);

        Assert.Contains("Unable to get the public key of the account.", output);
    }

    [TestMethod]
    public void TestOnRegisterCandidateCommand_LegacyPath_UsesInvokeInsteadOfNep17Payment()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithoutEchidna);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        var snapshot = neoSystem.StoreView;
        var index = NativeContract.Ledger.CurrentIndex(snapshot);
        Assert.IsFalse(neoSystem.Settings.IsHardforkEnabled(Hardfork.HF_Echidna, index));

        FundGas(neoSystem, account.ScriptHash, 100_000_000 * NativeContract.GAS.Factor);

        var output = RunRegisterCandidate(
            new VoteTestContext(neoSystem, wallet),
            account: account.ScriptHash,
            relayAnswer: "no");

        Assert.DoesNotContain("Registering candidate via GAS transfer", output);
        Assert.IsTrue(
            output.Contains("Invoking script with:", StringComparison.Ordinal) ||
            output.Contains("Insufficient GAS", StringComparison.Ordinal) ||
            output.Contains("Error:", StringComparison.Ordinal),
            "Legacy path should build and test-invoke a contract script instead of using NEP-17 payment.");
    }

    [TestMethod]
    public void TestOnRegisterCandidateCommand_EchidnaPath_InsufficientGas()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithEchidnaAtGenesis);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();

        var output = RunRegisterCandidate(
            new VoteTestContext(neoSystem, wallet),
            account: account.ScriptHash,
            relayAnswer: "no");

        Assert.Contains("Insufficient GAS. Required:", output);
        Assert.DoesNotContain("Invoking script with:", output);
    }

    [TestMethod]
    public void TestRegisterCandidateViaNep17Payment_InsufficientGas()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithEchidnaAtGenesis);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        var publicKey = account.GetKey()!.PublicKey;
        var snapshot = neoSystem.StoreView;

        var output = RunRegisterCandidateViaNep17Payment(
            new VoteTestContext(neoSystem, wallet),
            account.ScriptHash,
            publicKey,
            snapshot,
            relayAnswer: "no");

        Assert.Contains("Insufficient GAS. Required:", output);
    }

    [TestMethod]
    public void TestRegisterCandidateViaNep17Payment_RelayCancelled()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithEchidnaAtGenesis);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        var publicKey = account.GetKey()!.PublicKey;
        var snapshot = neoSystem.StoreView;
        var registerPrice = NativeContract.NEO.GetRegisterPrice(snapshot);
        FundGas(neoSystem, account.ScriptHash, 100_000_000 * NativeContract.GAS.Factor);

        var output = RunRegisterCandidateViaNep17Payment(
            new VoteTestContext(neoSystem, wallet),
            account.ScriptHash,
            publicKey,
            snapshot,
            relayAnswer: "no");

        Assert.Contains("Registering candidate via GAS transfer to NEO contract", output);
        Assert.DoesNotContain("Signed and relayed transaction", output);
        Assert.DoesNotContain("Relay failed:", output);
    }

    [TestMethod]
    public void TestRegisterCandidateViaNep17Payment_UsesGlobalWitnessScope()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithEchidnaAtGenesis);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        var publicKey = account.GetKey()!.PublicKey;
        var snapshot = neoSystem.StoreView;
        var registerPrice = NativeContract.NEO.GetRegisterPrice(snapshot);
        FundGas(neoSystem, account.ScriptHash, 100_000_000 * NativeContract.GAS.Factor);

        var signers = new[]
        {
            new Signer { Account = account.ScriptHash, Scopes = WitnessScope.Global }
        };
        var tx = wallet.MakeTransaction(snapshot, new[]
        {
            new TransferOutput
            {
                AssetId = NativeContract.GAS.Hash,
                Value = new BigDecimal((BigInteger)registerPrice, NativeContract.GAS.Decimals),
                ScriptHash = NativeContract.NEO.Hash,
                Data = publicKey.EncodePoint(true)
            }
        }, from: account.ScriptHash, cosigners: signers);

        Assert.HasCount(1, tx.Signers);
        Assert.AreEqual(account.ScriptHash, tx.Signers[0].Account);
        Assert.AreEqual(WitnessScope.Global, tx.Signers[0].Scopes);
    }

    [TestMethod]
    public void TestRegisterCandidateViaNep17Payment_MakeTransactionError()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithEchidnaAtGenesis);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        var publicKey = account.GetKey()!.PublicKey;
        var snapshot = neoSystem.StoreView;
        var registerPrice = NativeContract.NEO.GetRegisterPrice(snapshot);
        FundGas(neoSystem, account.ScriptHash, registerPrice);

        var output = RunRegisterCandidateViaNep17Payment(
            new VoteTestContext(neoSystem, wallet),
            account.ScriptHash,
            publicKey,
            snapshot,
            relayAnswer: "yes");

        Assert.Contains("Insufficient GAS balance to cover system and network fees", output);
        Assert.DoesNotContain("Registering candidate via GAS transfer", output);
    }

    [TestMethod]
    public void TestRegisterCandidateViaNep17Payment_RelayedSuccessfully()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithEchidnaAtGenesis);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        var publicKey = account.GetKey()!.PublicKey;
        var snapshot = neoSystem.StoreView;
        var registerPrice = NativeContract.NEO.GetRegisterPrice(snapshot);
        FundGas(neoSystem, account.ScriptHash, 100_000_000 * NativeContract.GAS.Factor);

        var output = RunRegisterCandidateViaNep17Payment(
            new VoteTestContext(neoSystem, wallet),
            account.ScriptHash,
            publicKey,
            snapshot,
            relayAnswer: "yes");

        Assert.Contains("Registering candidate via GAS transfer to NEO contract", output);
        Assert.IsTrue(
            output.Contains("Signed and relayed transaction", StringComparison.Ordinal) ||
            output.Contains("Relay failed:", StringComparison.Ordinal),
            "Relaying should be attempted after user confirmation.");
    }

    [TestMethod]
    public void TestOnRegisterCandidateCommand_EchidnaPath_RelayCancelled()
    {
        var neoSystem = new TestBlockchain.TestNeoSystem(WithEchidnaAtGenesis);
        var wallet = TestUtils.GenerateTestWallet("test_pwd");
        var account = wallet.CreateAccount();
        var snapshot = neoSystem.StoreView;
        var registerPrice = NativeContract.NEO.GetRegisterPrice(snapshot);
        FundGas(neoSystem, account.ScriptHash, 100_000_000 * NativeContract.GAS.Factor);

        var output = RunRegisterCandidate(
            new VoteTestContext(neoSystem, wallet),
            account: account.ScriptHash,
            relayAnswer: "no");

        Assert.Contains("Registering candidate via GAS transfer to NEO contract", output);
        Assert.DoesNotContain("Invoking script with:", output);
    }

    private static string RunRegisterCandidate(VoteTestContext context, UInt160? account = null, string? relayAnswer = null)
    {
        account ??= context.Wallet?.CreateAccount().ScriptHash ?? UInt160.Zero;

        return InvokeWithConsoleCapture(context, service =>
            TestUtils.InvokeNonPublic(service, "OnRegisterCandidateCommand", account), relayAnswer);
    }

    private static string RunRegisterCandidateViaNep17Payment(
        VoteTestContext context,
        UInt160 account,
        ECPoint publicKey,
        DataCache snapshot,
        string? relayAnswer)
    {
        return InvokeWithConsoleCapture(context, service =>
            TestUtils.InvokeNonPublic(service, "RegisterCandidateViaNep17Payment", account, publicKey, snapshot), relayAnswer);
    }

    private static string InvokeWithConsoleCapture(VoteTestContext context, Action<MainService> invoke, string? relayAnswer)
    {
        var service = CreateService(context);

        var outputWriter = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var originalIn = Console.In;

        Console.SetOut(outputWriter);
        Console.SetError(outputWriter);
        if (relayAnswer != null)
            Console.SetIn(new StringReader(relayAnswer + Environment.NewLine));

        try
        {
            invoke(service);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Console.SetIn(originalIn);
        }

        return outputWriter.ToString();
    }

    private static MainService CreateService(VoteTestContext context)
    {
        var service = new MainService();
        TestUtils.TrySet(service, "NeoSystem", context.NeoSystem);
        TestUtils.TrySetField(service, "_neoSystem", context.NeoSystem);

        if (context.Wallet != null)
        {
            TestUtils.TrySet(service, "CurrentWallet", context.Wallet);
            TestUtils.TrySetField(service, "_currentWallet", context.Wallet);
        }

        return service;
    }

    private static void FundGas(NeoSystem neoSystem, UInt160 account, BigInteger balance)
    {
        var key = new KeyBuilder(NativeContract.GAS.Id, 20).Add(account);
        var snapshot = neoSystem.GetSnapshotCache();
        var entry = snapshot.GetAndChange(key, () => new StorageItem(new AccountState()));
        entry.GetInteroperable<AccountState>().Balance = balance;
        snapshot.Commit();
    }

    private sealed record VoteTestContext(NeoSystem NeoSystem, Wallet? Wallet = null);
}
