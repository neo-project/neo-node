// Copyright (C) 2015-2026 The Neo Project.
//
// UT_Nep17API.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Moq;
using Neo.Extensions;
using Neo.Json;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Numerics;

namespace Neo.Network.RPC.Tests;

[TestClass]
public class UT_Nep17API
{
    Mock<RpcClient> rpcClientMock = null!;
    KeyPair keyPair1 = null!;
    UInt160 sender = null!;
    Nep17API nep17API = null!;

    [TestInitialize]
    public void TestSetup()
    {
        keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
        sender = Contract.CreateSignatureRedeemScript(keyPair1.PublicKey).ToScriptHash();
        rpcClientMock = UT_TransactionManager.MockRpcClient(sender, []);
        nep17API = new Nep17API(rpcClientMock.Object);
    }

    [TestMethod]
    public async Task TestBalanceOf()
    {
        byte[] testScript = NativeContract.Governance.Hash.MakeScript("balanceOf", UInt160.Zero);
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(10000) });

        var balance = await nep17API.BalanceOfAsync(NativeContract.Governance.Hash, UInt160.Zero);
        Assert.AreEqual(10000, (int)balance);
    }

    [TestMethod]
    public async Task TestGetSymbol()
    {
        byte[] testScript = NativeContract.Governance.Hash.MakeScript("symbol");
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.String, Value = Governance.GasTokenSymbol });

        var result = await nep17API.SymbolAsync(NativeContract.Governance.Hash);
        Assert.AreEqual(Governance.GasTokenSymbol, result);
    }

    [TestMethod]
    public async Task TestGetDecimals()
    {
        byte[] testScript = NativeContract.Governance.Hash.MakeScript("decimals");
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(Governance.GasTokenDecimals) });

        var result = await nep17API.DecimalsAsync(NativeContract.Governance.Hash);
        Assert.AreEqual(Governance.GasTokenDecimals, result);
    }

    [TestMethod]
    public async Task TestGetTotalSupply()
    {
        byte[] testScript = NativeContract.Governance.Hash.MakeScript("totalSupply");
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

        var result = await nep17API.TotalSupplyAsync(NativeContract.Governance.Hash);
        Assert.AreEqual(1_00000000, (int)result);
    }

    [TestMethod]
    public async Task TestGetTokenInfo()
    {
        UInt160 gasTokenId = NativeContract.Governance.GasTokenId;
        byte[] testScript = [
            .. gasTokenId.MakeScript("symbol"),
            .. gasTokenId.MakeScript("decimals"),
            .. gasTokenId.MakeScript("totalSupply")];
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript,
            new ContractParameter { Type = ContractParameterType.String, Value = Governance.GasTokenSymbol },
            new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(Governance.GasTokenDecimals) },
            new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

        UInt160 neoTokenId = NativeContract.Governance.NeoTokenId;
        testScript = [
            .. neoTokenId.MakeScript("symbol"),
            .. neoTokenId.MakeScript("decimals"),
            .. neoTokenId.MakeScript("totalSupply")];
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript,
            new ContractParameter { Type = ContractParameterType.String, Value = Governance.NeoTokenSymbol },
            new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(Governance.NeoTokenDecimals) },
            new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

        var tests = TestUtils.RpcTestCases.Where(p => p.Name == "getcontractstateasync");
        var haveGasTokenUT = false;
        var haveNeoTokenUT = false;
        foreach (var test in tests)
        {
            rpcClientMock
                .Setup(p => p.RpcSendAsync("getcontractstate", It.Is<JToken?[]>(u => true)))
                .ReturnsAsync(test.Response.Result!)
                .Verifiable();

            var requested = test.Request.Params[0]!.AsString();

            if (requested == gasTokenId.ToString() ||
                requested.Equals(Governance.GasTokenName, StringComparison.OrdinalIgnoreCase))
            {
                var result = await nep17API.GetTokenInfoAsync(Governance.GasTokenName.ToLowerInvariant());
                Assert.AreEqual(Governance.GasTokenSymbol, result.Symbol);
                Assert.AreEqual(Governance.GasTokenDecimals, result.Decimals);
                Assert.AreEqual(1_00000000, (int)result.TotalSupply);
                Assert.AreEqual(Governance.GasTokenName, result.Name);

                result = await nep17API.GetTokenInfoAsync(gasTokenId);
                Assert.AreEqual(Governance.GasTokenSymbol, result.Symbol);
                Assert.AreEqual(Governance.GasTokenDecimals, result.Decimals);
                Assert.AreEqual(1_00000000, (int)result.TotalSupply);
                Assert.AreEqual(Governance.GasTokenName, result.Name);

                haveGasTokenUT = true;
            }
            else if (requested == neoTokenId.ToString() ||
                     requested.Equals(Governance.NeoTokenName, StringComparison.OrdinalIgnoreCase))
            {
                var result = await nep17API.GetTokenInfoAsync(Governance.NeoTokenName.ToLowerInvariant());
                Assert.AreEqual(Governance.NeoTokenSymbol, result.Symbol);
                Assert.AreEqual(Governance.NeoTokenDecimals, result.Decimals);
                Assert.AreEqual(1_00000000, (int)result.TotalSupply);
                Assert.AreEqual(Governance.NeoTokenName, result.Name);

                result = await nep17API.GetTokenInfoAsync(neoTokenId);
                Assert.AreEqual(Governance.NeoTokenSymbol, result.Symbol);
                Assert.AreEqual(Governance.NeoTokenDecimals, result.Decimals);
                Assert.AreEqual(1_00000000, (int)result.TotalSupply);
                Assert.AreEqual(Governance.NeoTokenName, result.Name);

                haveNeoTokenUT = true;
            }
        }
        Assert.IsTrue(haveGasTokenUT && haveNeoTokenUT); //Update RpcTestCases.json
    }

    [TestMethod]
    public async Task TestTransfer()
    {
        byte[] testScript = NativeContract.Governance.Hash.MakeScript("transfer", sender, UInt160.Zero, new BigInteger(1_00000000), null)
            .Concat([(byte)OpCode.ASSERT])
            .ToArray();
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter());

        var client = rpcClientMock.Object;
        var result = await nep17API.CreateTransferTxAsync(NativeContract.Governance.Hash, keyPair1, UInt160.Zero, new BigInteger(1_00000000), null, true);

        testScript = NativeContract.Governance.Hash.MakeScript("transfer", sender, UInt160.Zero, new BigInteger(1_00000000), string.Empty)
            .Concat([(byte)OpCode.ASSERT])
            .ToArray();
        UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter());

        result = await nep17API.CreateTransferTxAsync(NativeContract.Governance.Hash, keyPair1, UInt160.Zero, new BigInteger(1_00000000), string.Empty, true);
        Assert.IsNotNull(result);
    }
}
