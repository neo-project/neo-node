// Copyright (C) 2015-2026 The Neo Project.
//
// UT_MainService_Network.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets.NEP6;
using System.Reflection;

namespace Neo.CLI.Tests;

[TestClass]
public class UT_MainService_Network
{
    private TestBlockchain.TestNeoSystem _neoSystem = null!;

    [TestInitialize]
    public void TestSetup()
    {
        _neoSystem = TestBlockchain.GetSystem();
    }

    private static MainService CreateService(NeoSystem system, NEP6Wallet? wallet = null)
    {
        var service = new MainService();
        SetField(service, "_neoSystem", system);
        if (wallet is not null) SetField(service, "_currentWallet", wallet);
        return service;
    }

    private static void SetField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' should exist on {target.GetType().Name}.");
        field!.SetValue(target, value);
    }

    private static object? InvokeNonPublic(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Method '{methodName}' should exist on {target.GetType().Name}.");
        return method!.Invoke(target, args);
    }

    private static string CaptureConsoleOut(Action action)
    {
        var prevOut = Console.Out;
        var prevErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        Console.SetError(sw);
        try { action(); }
        finally
        {
            Console.SetOut(prevOut);
            Console.SetError(prevErr);
        }
        return sw.ToString();
    }

    [TestMethod]
    public void OnListPendingCommand_NoHost_PrintsDisabledShape()
    {
        // Without a PendingValidUntilRelayHost registered, `list pending` should still produce
        // a valid JSON document reporting enabled=false and an empty list.
        var service = CreateService(_neoSystem);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnListPendingCommand"));

        Assert.IsFalse(string.IsNullOrWhiteSpace(output));
        var json = (JObject)JToken.Parse(output)!;
        Assert.IsFalse(json["enabled"]!.AsBoolean());
        Assert.AreEqual(0, json["count"]!.AsNumber());
        Assert.IsInstanceOfType(json["pending"], typeof(JArray));
    }

    [TestMethod]
    public void OnListPendingCommand_WithHostAndQueuedEntry_PrintsEntry()
    {
        // When a host is registered as a service, the CLI must list the queued entries with hash and VUB.
        var wallet = TestUtils.GenerateTestWallet("pwd");
        var account = wallet.CreateAccount();

        var host = new PendingValidUntilRelayHost(new MemoryStore(), new PendingValidUntilRelayConfiguration(true, 1u));
        _neoSystem.AddService(host);

        var snapshot = _neoSystem.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_neoSystem.Settings);
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 1,
            ValidUntilBlock = height + maxInc + 10,
            Signers = [new Signer { Account = account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        var ctx = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        Assert.IsTrue(wallet.Sign(ctx));
        tx.Witnesses = ctx.GetWitnesses();

        Assert.IsTrue(PendingValidUntilRelay.TryOffer(_neoSystem, host, tx, VerifyResult.Expired));

        var service = CreateService(_neoSystem, wallet);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnListPendingCommand"));

        var json = (JObject)JToken.Parse(output)!;
        Assert.IsTrue(json["enabled"]!.AsBoolean());
        Assert.AreEqual(1, json["count"]!.AsNumber());
        var arr = (JArray)json["pending"]!;
        Assert.AreEqual(1, arr.Count);
        Assert.AreEqual(tx.Hash.ToString(), arr[0]!["hash"]!.AsString());
    }

    [TestMethod]
    public void OnRelayCommand_NullInput_WarnsAndReturns()
    {
        var service = CreateService(_neoSystem);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnRelayCommand", new object?[] { null }));
        Assert.Contains("You must input JSON object to relay", output);
    }

    [TestMethod]
    public void OnRelayCommand_IncompleteSignature_PrintsError()
    {
        // A 2-of-2 multisig context that only has one signature -> Completed=false -> "signature is incomplete".
        var walletA = TestUtils.GenerateTestWallet("a");
        var walletB = TestUtils.GenerateTestWallet("b");
        var accountA = walletA.CreateAccount();
        var accountB = walletB.CreateAccount();

        var multiSig = Contract.CreateMultiSigContract(2, new[] { accountA.GetKey()!.PublicKey, accountB.GetKey()!.PublicKey });
        walletA.CreateAccount(multiSig, accountA.GetKey());

        var snapshot = _neoSystem.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_neoSystem.Settings);
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 7,
            ValidUntilBlock = height + maxInc,
            Signers = [new Signer { Account = multiSig.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        var ctx = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        Assert.IsTrue(walletA.Sign(ctx));
        Assert.IsFalse(ctx.Completed);

        var json = (JObject)JToken.Parse(ctx.ToString())!;
        var service = CreateService(_neoSystem);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnRelayCommand", json));
        Assert.Contains("signature is incomplete", output);
    }

    [TestMethod]
    public void OnRelayCommand_FarFutureValidUntil_QueuesLocally_WhenHostConfigured()
    {
        // Build a fully-signed tx whose ValidUntilBlock is beyond the allowed forward window.
        // Blockchain.Ask should reject it as Expired, and PendingValidUntilRelay.TryOffer should
        // pick it up and queue it locally (covering the queuedLocally branch in OnRelayCommand).
        var wallet = TestUtils.GenerateTestWallet("pwd");
        var account = wallet.CreateAccount();

        var host = new PendingValidUntilRelayHost(new MemoryStore(), new PendingValidUntilRelayConfiguration(true, 1u));
        _neoSystem.AddService(host);

        var snapshot = _neoSystem.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_neoSystem.Settings);
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 99,
            ValidUntilBlock = height + maxInc + 100,
            Signers = [new Signer { Account = account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        var ctx = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        Assert.IsTrue(wallet.Sign(ctx));
        Assert.IsTrue(ctx.Completed);

        var json = (JObject)JToken.Parse(ctx.ToString())!;
        var service = CreateService(_neoSystem, wallet);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnRelayCommand", json));

        Assert.Contains(PendingValidUntilRelay.CliQueuedLocallyHint, output,
            "Far-future tx should be reported as queued locally.");
        Assert.AreEqual(1, host.Store.Find(null).Count(), "Tx must end up in the local pending store.");
    }

    [TestMethod]
    public void OnRelayCommand_HappyPath_PrintsRelaySuccessOrFailure()
    {
        // Build a fully-signed tx within the allowed window. Blockchain.Ask will respond with some
        // VerifyResult (likely InsufficientFunds for our synthetic tx). Either Succeed -> "success"
        // message, or non-success without queuing -> "Relay failed" message: we accept any of the
        // two branches but require the exception-free, non-incomplete path to be taken.
        var wallet = TestUtils.GenerateTestWallet("pwd");
        var account = wallet.CreateAccount();

        var snapshot = _neoSystem.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(_neoSystem.Settings);
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 11,
            ValidUntilBlock = height + maxInc,
            Signers = [new Signer { Account = account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        var ctx = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        Assert.IsTrue(wallet.Sign(ctx));
        Assert.IsTrue(ctx.Completed);

        var json = (JObject)JToken.Parse(ctx.ToString())!;
        var service = CreateService(_neoSystem, wallet);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnRelayCommand", json));

        bool reportedSuccess = output.Contains("Data relay success");
        bool reportedFailure = output.Contains("Relay failed");
        Assert.IsTrue(reportedSuccess || reportedFailure,
            "OnRelayCommand should report either a relay success or a relay failure for an in-window tx.");
        Assert.DoesNotContain(PendingValidUntilRelay.CliQueuedLocallyHint, output,
            "In-window tx must not be queued locally.");
    }

    [TestMethod]
    public void OnRelayCommand_MalformedContext_PrintsExceptionMessage()
    {
        // A JObject that fails ContractParametersContext.Parse must surface as an error
        // line via the exception path, not throw out of the command handler.
        var service = CreateService(_neoSystem);
        var bogus = new JObject { ["foo"] = "bar" };
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnRelayCommand", bogus));
        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Should print an error explanation.");
    }

    [TestMethod]
    public void OnBroadcastBlockCommand_ByHash_NotFound_PrintsError()
    {
        var service = CreateService(_neoSystem);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnBroadcastGetBlocksByHashCommand", UInt256.Zero));
        Assert.Contains("Block is not found", output);
    }

    [TestMethod]
    public void OnBroadcastBlockCommand_ByHeight_NotFound_PrintsError()
    {
        // Pick a height past current; ledger lookup returns null and the command prints the error.
        var snapshot = _neoSystem.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        var service = CreateService(_neoSystem);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnBroadcastGetBlocksByHeightCommand", height + 9999));
        Assert.Contains("Block is not found", output);
    }

    [TestMethod]
    public void OnBroadcastAddressCommand_NullPayload_PrintsWarning()
    {
        // System.Net.IPAddress parameter; passing null exercises the input-validation branch.
        var service = CreateService(_neoSystem);
        var output = CaptureConsoleOut(() => InvokeNonPublic(service, "OnBroadcastAddressCommand", new object?[] { null, (ushort)10333 }));
        Assert.Contains("payload to relay", output);
    }
}
