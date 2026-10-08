// Copyright (C) 2015-2026 The Neo Project.
//
// UT_RpcServer.TransactionPreview.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Reflection;

namespace Neo.Plugins.RpcServer.Tests;

public partial class UT_RpcServer
{
    private JObject Preview(Transaction tx)
        => PreviewRaw(Convert.ToBase64String(tx.ToArray()));

    private JObject PreviewRaw(string encoded)
    {
        var method = typeof(RpcServer).GetMethod("InvokeTransaction", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "The signed-transaction preflight endpoint must exist.");
        try { return (JObject)method.Invoke(_rpcServer, [encoded]); }
        catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
    }

    private Transaction SignedPreviewTransaction(byte[] script = null, long systemFee = 1_000_000,
        WitnessScope scope = WitnessScope.None)
    {
        using var snapshot = _neoSystem.GetSnapshotCache();
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 0x8127,
            ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + 100,
            SystemFee = systemFee,
            NetworkFee = 20_000_000,
            Attributes = [],
            Signers = [new Signer { Account = _walletAccount.ScriptHash, Scopes = scope }],
            Script = script ?? [(byte)OpCode.PUSH1],
            Witnesses = []
        };
        var context = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        Assert.IsTrue(_wallet.Sign(context));
        tx.Witnesses = context.GetWitnesses();
        return tx;
    }

    [TestMethod]
    public void TransactionPreviewVerifiesExactSignatureWithoutRelaying()
    {
        var tx = SignedPreviewTransaction();
        int poolCount = _neoSystem.MemPool.Count;
        using var snapshot = _neoSystem.GetSnapshotCache();
        var before = NativeContract.GAS.BalanceOf(snapshot, _walletAccount.ScriptHash);
        var result = Preview(tx);
        Assert.AreEqual(tx.Hash.ToString(), result["hash"].GetString());
        Assert.AreEqual("Succeed", result["verification"].GetString());
        Assert.AreEqual("HALT", result["state"].GetString());
        Assert.AreEqual("1", result["stack"][0]["value"].GetString());
        Assert.IsFalse(result["relayed"].GetBoolean());
        Assert.IsFalse(result["mempoolChecked"].GetBoolean());
        Assert.AreEqual(poolCount, _neoSystem.MemPool.Count);
        using var after = _neoSystem.GetSnapshotCache();
        Assert.AreEqual(before, NativeContract.GAS.BalanceOf(after, _walletAccount.ScriptHash));
        tx.SystemFee += 1;
        result = Preview(tx);
        Assert.AreEqual("InvalidSignature", result["verification"].GetString());
        Assert.IsFalse(result.ContainsProperty("stack"));
    }

    [TestMethod]
    public void TransactionPreviewUsesSubmittedGasAndReportsApplicationFault()
    {
        var fault = Preview(SignedPreviewTransaction([(byte)OpCode.ABORT]));
        Assert.AreEqual("Succeed", fault["verification"].GetString());
        Assert.AreEqual("FAULT", fault["state"].GetString());
        var underfunded = Preview(SignedPreviewTransaction(systemFee: 0));
        Assert.AreEqual("Succeed", underfunded["verification"].GetString());
        Assert.AreEqual("FAULT", underfunded["state"].GetString());
    }

    [TestMethod]
    public void TransactionPreviewRejectsMalformedOversizedAndOverBudgetRequests()
    {
        foreach (var encoded in new[] { null, "", "not-base64", "AA==", new string('A', Transaction.MaxTransactionSize * 2) })
            Assert.ThrowsExactly<RpcException>(() => PreviewRaw(encoded));
        var tx = SignedPreviewTransaction();
        Assert.ThrowsExactly<RpcException>(() => PreviewRaw(Convert.ToBase64String([.. tx.ToArray(), 0])));
        Assert.ThrowsExactly<RpcException>(() => Preview(SignedPreviewTransaction(systemFee: _rpcServerSettings.MaxGasInvoke + 1)));
    }

    [TestMethod]
    public void TransactionPreviewBoundsStackSerializationByConfiguredItemSize()
    {
        using var builder = new ScriptBuilder();
        builder.EmitPush(new byte[256]);
        var tx = SignedPreviewTransaction(builder.ToArray());
        var unrestricted = Preview(tx);
        Assert.AreEqual("HALT", unrestricted["state"].GetString());
        Assert.AreEqual(256, Convert.FromBase64String(unrestricted["stack"][0]["value"].GetString()).Length);

        _rpcServerSettings = _rpcServerSettings with { MaxItemResponseSize = 64 };
        _rpcServer = new RpcServer(_neoSystem, _rpcServerSettings);
        var bounded = Preview(tx);
        Assert.AreEqual("Succeed", bounded["verification"].GetString());
        Assert.AreEqual("HALT", bounded["state"].GetString());
        Assert.AreEqual("error: result cannot be serialized", bounded["stack"].GetString());
        Assert.IsFalse(bounded["relayed"].GetBoolean());
    }

    [TestMethod]
    public void TransactionPreviewBoundsNotificationSerializationByConfiguredItemSize()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(NativeContract.GAS.Hash, "transfer", _walletAccount.ScriptHash,
            UInt160.Parse("0x0102030405060708091011121314151617181920"), 1, null);
        var tx = SignedPreviewTransaction(builder.ToArray(), 10_000_000, WitnessScope.CalledByEntry);
        var unrestricted = Preview(tx);
        Assert.AreEqual("HALT", unrestricted["state"].GetString());
        Assert.AreEqual("Transfer", unrestricted["notifications"][0]["eventname"].GetString());
        Assert.AreEqual(3, ((JArray)unrestricted["notifications"][0]["state"]["value"]).Count);

        _rpcServerSettings = _rpcServerSettings with { MaxItemResponseSize = 64 };
        _rpcServer = new RpcServer(_neoSystem, _rpcServerSettings);
        var bounded = Preview(tx);
        Assert.AreEqual("Succeed", bounded["verification"].GetString());
        Assert.AreEqual("HALT", bounded["state"].GetString());
        Assert.AreEqual("error: result cannot be serialized", bounded["notifications"].GetString());
        Assert.IsFalse(bounded["relayed"].GetBoolean());
    }
}
