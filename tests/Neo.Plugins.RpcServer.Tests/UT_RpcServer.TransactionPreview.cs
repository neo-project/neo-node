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

    private Transaction SignedPreviewTransaction(byte[] script = null, long systemFee = 1_000_000)
    {
        using var snapshot = _neoSystem.GetSnapshotCache();
        var tx = new Transaction
        {
            Version = 0, Nonce = 0x8127, ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + 100,
            SystemFee = systemFee, NetworkFee = 20_000_000, Attributes = [],
            Signers = [new Signer { Account = _walletAccount.ScriptHash, Scopes = WitnessScope.None }],
            Script = script ?? [(byte)OpCode.PUSH1], Witnesses = []
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
}
