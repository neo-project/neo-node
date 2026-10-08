// Copyright (C) 2015-2026 The Neo Project.
//
// UT_TransactionManager.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Moq;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Numerics;

namespace Neo.Network.RPC.Tests;

[TestClass]
public class UT_TransactionManager
{
    TransactionManager txManager;
    Mock<RpcClient> rpcClientMock;
    Mock<RpcClient> multiSigMock;
    KeyPair keyPair1;
    KeyPair keyPair2;
    UInt160 sender;
    UInt160 multiHash;
    RpcClient client;

    [TestInitialize]
    public void TestSetup()
    {
        keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
        keyPair2 = new KeyPair(Wallet.GetPrivateKeyFromWIF("L2LGkrwiNmUAnWYb1XGd5mv7v2eDf6P4F3gHyXSrNJJR4ArmBp7Q"));
        sender = Contract.CreateSignatureRedeemScript(keyPair1.PublicKey).ToScriptHash();
        multiHash = Contract.CreateMultiSigContract(2, new ECPoint[] { keyPair1.PublicKey, keyPair2.PublicKey }).ScriptHash;
        rpcClientMock = MockRpcClient(sender, new byte[1]);
        client = rpcClientMock.Object;
        multiSigMock = MockMultiSig(multiHash, new byte[1]);
    }

    public static Mock<RpcClient> MockRpcClient(UInt160 sender, byte[] script)
    {
        var mockRpc = new Mock<RpcClient>(MockBehavior.Strict, new Uri("http://seed1.neo.org:10331"), null, null, null);

        // MockHeight
        mockRpc.Setup(p => p.RpcSendAsync("getblockcount")).ReturnsAsync(100).Verifiable();

        // calculatenetworkfee
        var networkfee = new JObject() { ["networkfee"] = 100000000 };
        mockRpc.Setup(p => p.RpcSendAsync("calculatenetworkfee", It.Is<JToken[]>(u => true)))
            .ReturnsAsync(networkfee)
            .Verifiable();

        // MockGasBalance
        byte[] balanceScript = NativeContract.GAS.Hash.MakeScript("balanceOf", sender);
        var balanceResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("10000000000000000") };

        MockInvokeScript(mockRpc, balanceScript, balanceResult);

        // MockFeePerByte
        byte[] policyScript = NativeContract.Policy.Hash.MakeScript("getFeePerByte");
        var policyResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("1000") };

        MockInvokeScript(mockRpc, policyScript, policyResult);

        // MockGasConsumed
        var result = new ContractParameter();
        MockInvokeScript(mockRpc, script, result);

        return mockRpc;
    }

    public static Mock<RpcClient> MockMultiSig(UInt160 multiHash, byte[] script)
    {
        var mockRpc = new Mock<RpcClient>(MockBehavior.Strict, new Uri("http://seed1.neo.org:10331"), null, null, null);

        // MockHeight
        mockRpc.Setup(p => p.RpcSendAsync("getblockcount")).ReturnsAsync(100).Verifiable();

        // calculatenetworkfee
        var networkfee = new JObject() { ["networkfee"] = 100000000 };
        mockRpc.Setup(p => p.RpcSendAsync("calculatenetworkfee", It.Is<JToken[]>(u => true)))
            .ReturnsAsync(networkfee)
            .Verifiable();

        // MockGasBalance
        byte[] balanceScript = NativeContract.GAS.Hash.MakeScript("balanceOf", multiHash);
        var balanceResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("10000000000000000") };

        MockInvokeScript(mockRpc, balanceScript, balanceResult);

        // MockFeePerByte
        byte[] policyScript = NativeContract.Policy.Hash.MakeScript("getFeePerByte");
        var policyResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("1000") };

        MockInvokeScript(mockRpc, policyScript, policyResult);

        // MockGasConsumed
        var result = new ContractParameter();
        MockInvokeScript(mockRpc, script, result);

        return mockRpc;
    }

    public static void MockInvokeScript(Mock<RpcClient> mockClient, byte[] script, params ContractParameter[] parameters)
    {
        var result = new RpcInvokeResult()
        {
            Stack = parameters.Select(p => p.ToStackItem()).ToArray(),
            GasConsumed = 100,
            Script = Convert.ToBase64String(script),
            State = VMState.HALT
        };

        mockClient.Setup(p => p.RpcSendAsync("invokescript", It.Is<JToken[]>(j =>
            Convert.FromBase64String(j[0].AsString()).SequenceEqual(script))))
            .ReturnsAsync(result.ToJson())
            .Verifiable();
    }

    [TestMethod]
    public async Task TestMakeTransaction()
    {
        Signer[] signers = new Signer[1]
        {
            new Signer
            {
                Account = sender,
                Scopes= WitnessScope.Global
            }
        };

        byte[] script = new byte[1];
        txManager = await TransactionManager.MakeTransactionAsync(rpcClientMock.Object, script, signers);

        var tx = txManager.Tx;
        Assert.AreEqual(WitnessScope.Global, tx.Signers[0].Scopes);
    }

    [TestMethod]
    public async Task TransactionUsesRequiredAdmissionFeeAndRejectsFaultedSimulation()
    {
        byte[] script = [0];
        var response = new JObject
        {
            ["script"] = Convert.ToBase64String(script), ["state"] = "HALT", ["gasconsumed"] = "100",
            ["minimumrequiredfee"] = "100000100", ["stack"] = new JArray()
        };
        rpcClientMock.Setup(p => p.RpcSendAsync("invokescript", It.Is<JToken[]>(j =>
            Convert.FromBase64String(j[0].AsString()).SequenceEqual(script)))).ReturnsAsync(response);
        var factory = new TransactionManagerFactory(rpcClientMock.Object);
        var manager = await factory.MakeTransactionAsync(script, [new Signer { Account = sender, Scopes = WitnessScope.None }]);
        Assert.AreEqual(100000100L, manager.Tx.SystemFee);
        response["state"] = "FAULT";
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => factory.MakeTransactionAsync(script));
    }

    [TestMethod]
    public async Task TestSign()
    {
        Signer[] signers = new Signer[1]
        {
            new Signer
            {
                Account  =  sender,
                Scopes = WitnessScope.Global
            }
        };

        byte[] script = new byte[1];
        txManager = await TransactionManager.MakeTransactionAsync(client, script, signers);
        await txManager
            .AddSignature(keyPair1)
            .SignAsync();

        // get signature from Witnesses
        var tx = txManager.Tx;
        ReadOnlyMemory<byte> signature = tx.Witnesses[0].InvocationScript[2..];

        Assert.IsTrue(Crypto.VerifySignature(tx.GetSignData(client.protocolSettings.Network), signature.Span, keyPair1.PublicKey));
        // verify network fee and system fee
        Assert.AreEqual(100000000/*Mock*/, tx.NetworkFee);
        Assert.AreEqual(100, tx.SystemFee);

        // duplicate sign should not add new witness
        await ThrowsAsync<Exception>(async () => await txManager.AddSignature(keyPair1).SignAsync());

        // throw exception when the KeyPair is wrong
        await ThrowsAsync<Exception>(async () => await txManager.AddSignature(keyPair2).SignAsync());
    }

    // https://docs.microsoft.com/en-us/archive/msdn-magazine/2014/november/async-programming-unit-testing-asynchronous-code#testing-exceptions
    static async Task<TException> ThrowsAsync<TException>(Func<Task> action, bool allowDerivedTypes = true)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (allowDerivedTypes && !(ex is TException))
                throw new Exception("Delegate threw exception of type " +
                ex.GetType().Name + ", but " + typeof(TException).Name +
                " or a derived type was expected.", ex);
            if (!allowDerivedTypes && ex.GetType() != typeof(TException))
                throw new Exception("Delegate threw exception of type " +
                ex.GetType().Name + ", but " + typeof(TException).Name +
                " was expected.", ex);
            return (TException)ex;
        }
        throw new Exception("Delegate did not throw expected exception " +
        typeof(TException).Name + ".");
    }

    [TestMethod]
    public async Task TestSignMulti()
    {
        // Cosigner needs multi signature
        Signer[] signers = new Signer[1]
        {
            new Signer
            {
                Account = multiHash,
                Scopes = WitnessScope.Global
            }
        };

        byte[] script = new byte[1];
        txManager = await TransactionManager.MakeTransactionAsync(multiSigMock.Object, script, signers);
        await txManager
            .AddMultiSig(keyPair1, 2, keyPair1.PublicKey, keyPair2.PublicKey)
            .AddMultiSig(keyPair2, 2, keyPair1.PublicKey, keyPair2.PublicKey)
            .SignAsync();
    }

    [TestMethod]
    public async Task CustomWitnessIsPreservedInFeeQuote()
    {
        var custom = Contract.Create([ContractParameterType.Integer, ContractParameterType.ByteArray],
            [(byte)OpCode.DROP, (byte)OpCode.DROP, (byte)OpCode.PUSH1]);
        using var invocation = new ScriptBuilder();
        invocation.EmitPush(new byte[] { 7, 8 });
        invocation.EmitPush(42);
        int quotes = 0;
        rpcClientMock.Setup(p => p.RpcSendAsync("calculatenetworkfee", It.IsAny<JToken[]>()))
            .ReturnsAsync((string _, JToken[] args) =>
            {
                var quoted = Convert.FromBase64String(args[0].AsString()).AsSerializable<Transaction>();
                CollectionAssert.AreEqual(custom.Script, quoted.Witnesses[1].VerificationScript.ToArray());
                CollectionAssert.AreEqual(invocation.ToArray(), quoted.Witnesses[1].InvocationScript.ToArray());
                if (++quotes == 2) Assert.AreEqual(66, quoted.Witnesses[0].InvocationScript.Length);
                return new JObject { ["networkfee"] = 100000000 };
            });
        var manager = await TransactionManager.MakeTransactionAsync(client, new byte[] { (byte)OpCode.PUSH1 }, 100,
            [new Signer { Account = sender, Scopes = WitnessScope.None },
             new Signer { Account = custom.ScriptHash, Scopes = WitnessScope.None }]);
        var signed = await manager.AddSignature(keyPair1).AddWitness(custom, 42, new byte[] { 7, 8 }).SignAsync();
        Assert.AreEqual(2, quotes);
        CollectionAssert.AreEqual(custom.Script, signed.Witnesses[1].VerificationScript.ToArray());
    }

    [TestMethod]
    public async Task FinalWitnessFeeIncreaseDoesNotMutateSignedTransaction()
    {
        int quotes = 0;
        rpcClientMock.Setup(p => p.RpcSendAsync("calculatenetworkfee", It.IsAny<JToken[]>()))
            .ReturnsAsync(() => new JObject { ["networkfee"] = ++quotes == 1 ? 100000000 : 100000001 });
        var manager = await TransactionManager.MakeTransactionAsync(client, new byte[] { (byte)OpCode.PUSH1 }, 100,
            [new Signer { Account = sender, Scopes = WitnessScope.None }]);
        manager.AddSignature(keyPair1);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.SignAsync());
        Assert.AreEqual(100000000L, manager.Tx.NetworkFee);
        Assert.IsTrue(Crypto.VerifySignature(manager.Tx.GetSignData(client.protocolSettings.Network),
            manager.Tx.Witnesses[0].InvocationScript.Span[2..], keyPair1.PublicKey));
    }

    [TestMethod]
    public async Task TestAddWitness()
    {
        // Cosigner as contract scripthash
        Signer[] signers = new Signer[2]
        {
            new Signer
            {
                Account = sender,
                Scopes = WitnessScope.Global
            },
            new Signer
            {
                Account = UInt160.Zero,
                Scopes = WitnessScope.Global
            }
        };

        byte[] script = new byte[1];
        txManager = await TransactionManager.MakeTransactionAsync(rpcClientMock.Object, script, signers);
        txManager.AddWitness(UInt160.Zero);
        txManager.AddSignature(keyPair1);
        await txManager.SignAsync();

        var tx = txManager.Tx;
        Assert.HasCount(2, tx.Witnesses);
        Assert.AreEqual(40, tx.Witnesses[0].VerificationScript.Length);
        Assert.AreEqual(66, tx.Witnesses[0].InvocationScript.Length);
    }
}
