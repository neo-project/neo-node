// Copyright (C) 2015-2026 The Neo Project.
//
// UT_RpcServer.Wallet.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Neo.Plugins.RpcServer.Tests;

partial class UT_RpcServer
{
    private const string WalletJson = """
    {
        "name":null,
        "version":"1.0",
        "scrypt":{"n":16384, "r":8, "p":8 },
        "accounts":[{
            "address":"NVizn8DiExdmnpTQfjiVY3dox8uXg3Vrxv",
            "label":null,
            "isDefault":false,
            "lock":false,
            "key":"6PYPMrsCJ3D4AXJCFWYT2WMSBGF7dLoaNipW14t4UFAkZw3Z9vQRQV1bEU",
            "contract":{
                "script":"DCEDaR+FVb8lOdiMZ/wCHLiI+zuf17YuGFReFyHQhB80yMpBVuezJw==",
                "parameters":[{"name":"signature", "type":"Signature"}],
                "deployed":false
            },
            "extra":null
        }],
        "extra":null
    }
    """;

    [TestMethod]
    public void TestOpenWallet()
    {
        const string Path = "wallet-TestOpenWallet.json";
        const string Password = "123456";
        File.WriteAllText(Path, WalletJson);

        var res = _rpcServer.OpenWallet(Path, Password);
        Assert.IsTrue(res.AsBoolean());
        Assert.IsNotNull(_rpcServer.wallet);
        Assert.AreEqual("NVizn8DiExdmnpTQfjiVY3dox8uXg3Vrxv", _rpcServer.wallet.GetAccounts().FirstOrDefault()!.Address);

        _rpcServer.CloseWallet();
        File.Delete(Path);
        Assert.IsNull(_rpcServer.wallet);
    }

    [TestMethod]
    public void TestOpenInvalidWallet()
    {
        const string Path = "wallet-TestOpenInvalidWallet.json";
        const string Password = "password";
        File.Delete(Path);

        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.OpenWallet(Path, Password),
            "Should throw RpcException for unsupported wallet");
        Assert.AreEqual(RpcError.WalletNotFound.Code, exception.HResult);

        File.WriteAllText(Path, "{}");
        exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.OpenWallet(Path, Password),
            "Should throw RpcException for unsupported wallet");
        File.Delete(Path);
        Assert.AreEqual(RpcError.WalletNotSupported.Code, exception.HResult);

        var result = _rpcServer.CloseWallet();
        Assert.IsTrue(result.AsBoolean());
        Assert.IsNull(_rpcServer.wallet);

        File.WriteAllText(Path, WalletJson);
        exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.OpenWallet(Path, Password),
            "Should throw RpcException for unsupported wallet");
        Assert.AreEqual(RpcError.WalletNotSupported.Code, exception.HResult);
        Assert.AreEqual("Wallet not supported - Invalid password.", exception.Message);
        File.Delete(Path);
    }

    [TestMethod]
    public void TestDumpPrivKey()
    {
        TestUtilOpenWallet();
        var account = _rpcServer.wallet.GetAccounts().FirstOrDefault();
        Assert.IsNotNull(account);

        var privKey = account.GetKey().Export();
        var address = account.Address;
        var result = _rpcServer.DumpPrivKey(new JString(address).ToAddress(ProtocolSettings.Default.AddressVersion));
        Assert.AreEqual(privKey, result.AsString());
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestDumpPrivKey_AddressNotInWallet()
    {
        TestUtilOpenWallet();
        // Generate a valid address not in the wallet
        var key = new KeyPair(RandomNumberGenerator.GetBytes(32));
        // Correct way to get ScriptHash from PublicKey
        var scriptHashNotInWallet = Contract.CreateSignatureRedeemScript(key.PublicKey).ToScriptHash();
        var notFound = scriptHashNotInWallet.ToAddress(ProtocolSettings.Default.AddressVersion);

        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.DumpPrivKey(new JString(notFound).AsParameter<Address>()));
        Assert.AreEqual(RpcError.UnknownAccount.Code, ex.HResult);
        Assert.Contains($"Unknown account - {scriptHashNotInWallet}", ex.Message);
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestDumpPrivKey_InvalidAddressFormat()
    {
        TestUtilOpenWallet();
        var invalidAddress = "NotAValidAddress";
        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.DumpPrivKey(new JString(invalidAddress).AsParameter<Address>()));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestGetNewAddress()
    {
        TestUtilOpenWallet();
        var result = _rpcServer.GetNewAddress();
        Assert.IsInstanceOfType(result, typeof(JString));
        Assert.IsTrue(_rpcServer.wallet.GetAccounts().Any(a => a.Address == result.AsString()));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestGetWalletBalance()
    {
        TestUtilOpenWallet();
        var assetId = NativeContract.NEO.Hash;
        var result = _rpcServer.GetWalletBalance(assetId);
        Assert.IsInstanceOfType(result, typeof(JObject));

        var json = (JObject)result;
        Assert.IsTrue(json.ContainsProperty("balance"));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestGetWalletBalanceInvalidAsset()
    {
        TestUtilOpenWallet();
        var assetId = UInt160.Zero;
        var result = _rpcServer.GetWalletBalance(assetId);
        Assert.IsInstanceOfType(result, typeof(JObject));

        var json = (JObject)result;
        Assert.IsTrue(json.ContainsProperty("balance"));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestGetWalletBalance_InvalidAssetIdFormat()
    {
        TestUtilOpenWallet();
        var invalidAssetId = "NotAValidAssetID";

        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.GetWalletBalance(new JString(invalidAssetId).AsParameter<UInt160>()));
        Assert.AreEqual(RpcError.InvalidParams.Code, ex.HResult);
        Assert.Contains("Invalid UInt160", ex.Message);
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestGetWalletUnclaimedGas()
    {
        TestUtilOpenWallet();
        var result = _rpcServer.GetWalletUnclaimedGas();
        Assert.IsInstanceOfType(result, typeof(JString));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestImportPrivKey()
    {
        TestUtilOpenWallet();
        var privKey = _walletAccount.GetKey().Export();
        var result = _rpcServer.ImportPrivKey(privKey);
        Assert.IsInstanceOfType(result, typeof(JObject));

        var json = (JObject)result;
        Assert.IsTrue(json.ContainsProperty("address"));
        Assert.IsTrue(json.ContainsProperty("haskey"));
        Assert.IsTrue(json.ContainsProperty("label"));
        Assert.IsTrue(json.ContainsProperty("watchonly"));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestImportPrivKeyNoWallet()
    {
        var privKey = _walletAccount.GetKey().Export();
        var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.ImportPrivKey(privKey));
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestImportPrivKey_InvalidWIF()
    {
        TestUtilOpenWallet();
        var invalidWif = "ThisIsAnInvalidWIFString";

        // Expect FormatException during WIF decoding
        var ex = Assert.ThrowsExactly<FormatException>(() => _rpcServer.ImportPrivKey(invalidWif));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestImportPrivKey_KeyAlreadyExists()
    {
        TestUtilOpenWallet();

        // Get a key already in the default test wallet
        var existingAccount = _rpcServer.wallet.GetAccounts().First(a => a.HasKey);
        var existingWif = existingAccount.GetKey().Export();

        // Import the existing key
        var result = (JObject)_rpcServer.ImportPrivKey(existingWif);

        // Verify the returned account details match the existing one
        Assert.AreEqual(existingAccount.Address, result["address"].AsString());
        Assert.AreEqual(existingAccount.HasKey, result["haskey"].AsBoolean());
        Assert.AreEqual(existingAccount.Label, result["label"]?.AsString());
        Assert.AreEqual(existingAccount.WatchOnly, result["watchonly"].AsBoolean());

        // Ensure no duplicate account was created (check count remains same)
        var initialCount = _rpcServer.wallet.GetAccounts().Count();
        Assert.AreEqual(initialCount, _rpcServer.wallet.GetAccounts().Count(), "Account count should not change when importing existing key.");

        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSignMsgAndVerify()
    {
        TestUtilOpenWallet();

        var message = "hello rpc wallet";
        var signResult = (JObject)_rpcServer.SignMsg(message);

        Assert.IsTrue(signResult.ContainsProperty("payload"));
        Assert.IsTrue(signResult.ContainsProperty("signatures"));

        var signatures = (JArray)signResult["signatures"];
        Assert.IsGreaterThan(0, signatures.Count);

        var first = (JObject)signatures[0];
        var verifyResult = (JObject)_rpcServer.VerifyMsg(
            message,
            first["signature"].AsString(),
            first["publickey"].AsString(),
            first["salt"].AsString());

        Assert.AreEqual("Valid", verifyResult["status"].AsString());
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestVerify_InvalidSignature()
    {
        TestUtilOpenWallet();

        var signResult = (JObject)_rpcServer.SignMsg("hello");
        var first = (JObject)((JArray)signResult["signatures"])[0];
        var originalSignature = first["signature"].AsString();
        var tamperedSignature = new string('0', originalSignature.Length);

        var verifyResult = (JObject)_rpcServer.VerifyMsg(
            "hello",
            tamperedSignature,
            first["publickey"].AsString(),
            first["salt"].AsString());

        Assert.AreEqual("Invalid", verifyResult["status"].AsString());
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSignMsg_NoWallet()
    {
        _rpcServer.wallet = null;
        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.SignMsg("hello"));
        Assert.AreEqual(RpcError.NoOpenedWallet.Code, ex.HResult);
    }

    [TestMethod]
    public void TestSignMsgAndVerify_AvoidSignatureReplay()
    {
        TestUtilOpenWallet();

        var message = "hello replay-safe";
        var signResult = (JObject)_rpcServer.SignMsg(message, avoidSignatureReplay: true);
        var signatures = (JArray)signResult["signatures"];
        Assert.IsGreaterThan(0, signatures.Count);

        var first = (JObject)signatures[0];
        var verifyResult = (JObject)_rpcServer.VerifyMsg(
            message,
            first["signature"].AsString(),
            first["publickey"].AsString(),
            first["salt"].AsString(),
            avoidSignatureReplay: true);

        Assert.AreEqual("Valid", verifyResult["status"].AsString());
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestVerify_AvoidSignatureReplay_InvalidSignature()
    {
        TestUtilOpenWallet();

        var message = "hello replay-safe";
        var signResult = (JObject)_rpcServer.SignMsg(message, avoidSignatureReplay: true);
        var first = (JObject)((JArray)signResult["signatures"])[0];
        var originalSignature = first["signature"].AsString();
        var tamperedSignature = new string('f', originalSignature.Length);

        var verifyResult = (JObject)_rpcServer.VerifyMsg(
            message,
            tamperedSignature,
            first["publickey"].AsString(),
            first["salt"].AsString(),
            avoidSignatureReplay: true);

        Assert.AreEqual("Invalid", verifyResult["status"].AsString());
        TestUtilCloseWallet();
    }

    [TestMethod]
    public async Task TestVerifyMsg_RpcMethodName()
    {
        TestUtilOpenWallet();
        var message = "hello verifymsg";
        var signResult = (JObject)_rpcServer.SignMsg(message);
        var first = (JObject)((JArray)signResult["signatures"])[0];

        var context = new DefaultHttpContext();
        var request = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "verifymsg",
            ["params"] = new JArray
            {
                message,
                first["signature"],
                first["publickey"],
                first["salt"]
            }
        };

        var response = await _rpcServer.ProcessRequestAsync(context, request);
        Assert.IsNotNull(response);
        Assert.IsNotNull(response["result"]);
        Assert.IsNull(response["error"]);
        Assert.AreEqual("Valid", response["result"]["status"].AsString());
        TestUtilCloseWallet();
    }

    [TestMethod]
    public async Task TestVerify_RpcMethodRemoved()
    {
        var context = new DefaultHttpContext();
        var request = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "verify",
            ["params"] = new JArray { "m", "s", "p", "salt" }
        };

        var response = await _rpcServer.ProcessRequestAsync(context, request);
        Assert.IsNotNull(response);
        Assert.IsNotNull(response["error"]);
        Assert.AreEqual(RpcError.MethodNotFound.Code, response["error"]["code"].AsNumber());
    }

    [TestMethod]
    public void TestCalculateNetworkFee()
    {
        var snapshot = _neoSystem.GetSnapshotCache();
        var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
        var result = _rpcServer.CalculateNetworkFee(tx.ToArray());
        Assert.IsInstanceOfType(result, typeof(JObject));

        var json = (JObject)result;
        Assert.IsTrue(json.ContainsProperty("networkfee"));
    }

    [TestMethod]
    public void TestCalculateNetworkFeeNoParam()
    {
        var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.CalculateNetworkFee([]));
        Assert.AreEqual(exception.HResult, RpcError.InvalidParams.Code);
    }

    [TestMethod]
    public void TestListAddressNoWallet()
    {
        var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.ListAddress());
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestListAddress()
    {
        TestUtilOpenWallet();
        var result = _rpcServer.ListAddress();
        Assert.IsInstanceOfType(result, typeof(JArray));

        var json = (JArray)result;
        Assert.IsGreaterThan(0, json.Count);
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSendFromNoWallet()
    {
        var assetId = NativeContract.GAS.Hash;
        var from = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var to = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var amount = "1";
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.SendFrom(assetId, from, to, amount),
             "Should throw RpcException for insufficient funds");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestSendFrom()
    {
        TestUtilOpenWallet();

        var assetId = NativeContract.GAS.Hash;
        var from = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var to = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var amount = "1";
        var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendFrom(assetId, from, to, amount));
        Assert.AreEqual(exception.HResult, RpcError.InvalidRequest.Code);

        TestUtilCloseWallet();

        _rpcServer.wallet = _wallet;
        var resp = (JObject)_rpcServer.SendFrom(assetId, from, to, amount);
        Assert.AreEqual(12, resp.Count);
        Assert.AreEqual(resp["sender"], ValidatorAddress);

        var signers = (JArray)resp["signers"];
        Assert.HasCount(1, signers);
        Assert.AreEqual(signers[0]["account"], ValidatorScriptHash.ToString());
        Assert.AreEqual(nameof(WitnessScope.CalledByEntry), signers[0]["scopes"]);
        _rpcServer.wallet = null;
    }

    [TestMethod]
    public void TestSendMany()
    {
        var from = _walletAccount.Address;
        var to = new JArray {
            new JObject { ["asset"] = NativeContract.GAS.Hash.ToString(), ["value"] = "1", ["address"] = _walletAccount.Address }
        };

        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.SendMany(new JArray(from, to)),
            "Should throw RpcException for insufficient funds");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);

        _rpcServer.wallet = _wallet;
        var resp = (JObject)_rpcServer.SendMany(new JArray(from, to));
        Assert.AreEqual(12, resp.Count);
        Assert.AreEqual(resp["sender"], ValidatorAddress);

        var signers = (JArray)resp["signers"];
        Assert.HasCount(1, signers);
        Assert.AreEqual(signers[0]["account"], ValidatorScriptHash.ToString());
        Assert.AreEqual(nameof(WitnessScope.CalledByEntry), signers[0]["scopes"]);
        _rpcServer.wallet = null;
    }

    [TestMethod]
    public void TestSendToAddress()
    {
        var assetId = NativeContract.GAS.Hash;
        var to = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var amount = "1";
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.SendToAddress(assetId, to, amount),
            "Should throw RpcException for insufficient funds");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);

        _rpcServer.wallet = _wallet;
        var resp = (JObject)_rpcServer.SendToAddress(assetId, to, amount);
        Assert.AreEqual(12, resp.Count);
        Assert.AreEqual(resp["sender"], ValidatorAddress);

        var signers = (JArray)resp["signers"];
        Assert.HasCount(1, signers);
        Assert.AreEqual(signers[0]["account"], ValidatorScriptHash.ToString());
        Assert.AreEqual(nameof(WitnessScope.CalledByEntry), signers[0]["scopes"]);
        _rpcServer.wallet = null;
    }

    [TestMethod]
    public void TestSendToAddress_InvalidAssetId()
    {
        TestUtilOpenWallet();
        var invalidAssetId = "NotAnAssetId";
        var to = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var amount = "1";

        var ex = Assert.ThrowsExactly<RpcException>(
            () => _rpcServer.SendToAddress(new JString(invalidAssetId).AsParameter<UInt160>(), to, amount));
        Assert.AreEqual(RpcError.InvalidParams.Code, ex.HResult);
        Assert.Contains("Invalid UInt160", ex.Message);
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSendToAddress_InvalidToAddress()
    {
        TestUtilOpenWallet();
        var assetId = NativeContract.GAS.Hash;
        var invalidToAddress = "NotAnAddress";
        var amount = "1";

        var ex = Assert.ThrowsExactly<RpcException>(
            () => _rpcServer.SendToAddress(assetId, new JString(invalidToAddress).AsParameter<Address>(), amount));

        // Expect FormatException from AddressToScriptHash
        Assert.AreEqual(RpcError.InvalidParams.Code, ex.HResult);
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSendToAddress_NegativeAmount()
    {
        TestUtilOpenWallet();
        var assetId = NativeContract.GAS.Hash;
        var to = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var amount = "-1";

        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.SendToAddress(assetId, to, amount));
        Assert.AreEqual(RpcError.InvalidParams.Code, ex.HResult);
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSendToAddress_ZeroAmount()
    {
        TestUtilOpenWallet();
        var assetId = NativeContract.GAS.Hash;
        var to = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var amount = "0";

        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.SendToAddress(assetId, to, amount));
        Assert.AreEqual(RpcError.InvalidParams.Code, ex.HResult);
        // Implementation checks amount.Sign > 0
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSendToAddress_InsufficientFunds()
    {
        TestUtilOpenWallet();
        var assetId = NativeContract.GAS.Hash;

        var to = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var hugeAmount = "100000000000000000"; // Exceeds likely balance

        // With a huge amount, MakeTransaction might throw InvalidOperationException internally
        // before returning null to trigger the InsufficientFunds RpcException.
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => _rpcServer.SendToAddress(assetId, to, hugeAmount));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSendMany_InvalidFromAddress()
    {
        TestUtilOpenWallet();
        var invalidFrom = "NotAnAddress";
        var to = new JArray {
            new JObject { ["asset"] = NativeContract.GAS.Hash.ToString(), ["value"] = "1", ["address"] = _walletAccount.Address }
        };

        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.SendMany(new JArray(invalidFrom, to)));
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSendMany_EmptyOutputs()
    {
        TestUtilOpenWallet();
        var from = _walletAccount.Address;
        var emptyTo = new JArray(); // Empty output array
        var paramsArray = new JArray(from, emptyTo);

        var ex = Assert.ThrowsExactly<RpcException>(() => _rpcServer.SendMany(paramsArray));
        Assert.AreEqual(RpcError.InvalidParams.Code, ex.HResult);
        Assert.Contains("Argument 'to' can't be empty", ex.Message);
        TestUtilCloseWallet();
    }

    [TestMethod]
    public void TestSign_WhenWalletNotOpen()
    {
        _rpcServer.wallet = null;
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.Sign(new JObject()),
            "Should throw RpcException for no opened wallet");
        Assert.AreEqual(RpcError.NoOpenedWallet.Code, exception.HResult);
    }

    [TestMethod]
    public void TestSign_NullContext()
    {
        _rpcServer.wallet = _wallet;
        try
        {
            var exception = Assert.ThrowsExactly<RpcException>(
                () => _ = _rpcServer.Sign(null),
                "Should throw RpcException when JSON object is null");
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
        }
        finally
        {
            _rpcServer.wallet = null;
        }
    }

    [TestMethod]
    public void TestSign_InvalidContextJson()
    {
        _rpcServer.wallet = _wallet;
        try
        {
            var bogus = new JObject { ["foo"] = "bar" };
            var exception = Assert.ThrowsExactly<RpcException>(
                () => _ = _rpcServer.Sign(bogus),
                "Should throw RpcException when JSON object is not a valid ContractParametersContext");
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
            Assert.Contains("Invalid signature context", exception.Message);
        }
        finally
        {
            _rpcServer.wallet = null;
        }
    }

    [TestMethod]
    public void TestSign_NetworkMismatch()
    {
        _rpcServer.wallet = _wallet;
        try
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
            tx.Witnesses = [];
            // Sign on the correct network and then rewrite "network" so the JSON looks like
            // a context produced for a different network (e.g. mainnet vs testnet).
            var ctx = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
            Assert.IsTrue(_wallet.Sign(ctx));
            var contextJson = (JObject)ctx.ToJson();
            contextJson["network"] = _neoSystem.Settings.Network + 1;

            var exception = Assert.ThrowsExactly<RpcException>(
                () => _ = _rpcServer.Sign(contextJson),
                "Should throw RpcException on network mismatch");
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
            Assert.Contains("Network mismatch", exception.Message);
        }
        finally
        {
            _rpcServer.wallet = null;
        }
    }

    [TestMethod]
    public void TestSign_NoMatchingPrivateKey()
    {
        // A wallet that contains no private keys for the signers of the context.
        var emptyWallet = TestUtils.GenerateTestWallet("empty");
        _rpcServer.wallet = emptyWallet;
        try
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
            tx.Witnesses = [];
            var unsigned = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
            var contextJson = (JObject)unsigned.ToJson();

            var exception = Assert.ThrowsExactly<RpcException>(
                () => _ = _rpcServer.Sign(contextJson),
                "Should throw RpcException when wallet has no matching private key");
            Assert.AreEqual(RpcError.BadRequest.Code, exception.HResult);
            Assert.Contains("Non-existent private key", exception.Message);
        }
        finally
        {
            _rpcServer.wallet = null;
        }
    }

    [TestMethod]
    public void TestSign_CompleteSingleSignature()
    {
        // Verify that an unsigned context produced by another node (e.g. CLI `sign`)
        // can be fed into the RPC `sign` and gets fully signed.
        _rpcServer.wallet = _wallet;
        try
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
            tx.Witnesses = [];
            var unsigned = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
            Assert.IsNull(unsigned.GetSignatures(tx.Sender));
            var contextJson = (JObject)unsigned.ToJson();

            var result = _rpcServer.Sign(contextJson);
            Assert.IsInstanceOfType(result, typeof(JObject));

            var signed = ContractParametersContext.Parse(result.ToString(), snapshot);
            Assert.IsTrue(signed.Completed, "Context should be complete after signing with the only required key.");
            Assert.HasCount(1, signed.GetSignatures(tx.Sender));
        }
        finally
        {
            _rpcServer.wallet = null;
        }
    }

    [TestMethod]
    public void TestSign_PartialMultiSignature()
    {
        // Build a 2-of-3 multisig contract where each share lives in a separate wallet.
        // The CLI of one node would produce a partial context for the first signer, and
        // another node's RPC `sign` should be able to add the second signature.
        var walletA = TestUtils.GenerateTestWallet("a");
        var walletB = TestUtils.GenerateTestWallet("b");
        var walletC = TestUtils.GenerateTestWallet("c");

        var accountA = walletA.CreateAccount();
        var accountB = walletB.CreateAccount();
        var accountC = walletC.CreateAccount();

        var publicKeys = new[]
        {
            accountA.GetKey().PublicKey,
            accountB.GetKey().PublicKey,
            accountC.GetKey().PublicKey,
        };

        var multiSigContract = Contract.CreateMultiSigContract(2, publicKeys);
        walletA.CreateAccount(multiSigContract, accountA.GetKey());
        walletB.CreateAccount(multiSigContract, accountB.GetKey());
        walletC.CreateAccount(multiSigContract, accountC.GetKey());

        var snapshot = _neoSystem.GetSnapshotCache();
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 12345,
            ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + _neoSystem.Settings.MaxValidUntilBlockIncrement,
            Signers = [new Signer { Account = multiSigContract.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new[] { (byte)OpCode.RET },
            Witnesses = [],
            SystemFee = 0,
            NetworkFee = 1_000_000,
        };

        // Step 1: walletA signs locally (simulating CLI `sign` on node A).
        var contextA = new ContractParametersContext(snapshot, tx, _neoSystem.Settings.Network);
        Assert.IsTrue(walletA.Sign(contextA));
        Assert.IsFalse(contextA.Completed, "2-of-3 multisig should not be complete after a single signature.");

        // Step 2: hand the JSON over to the RPC of node B and let it add the second signature.
        _rpcServer.wallet = walletB;
        try
        {
            var partialJson = (JObject)contextA.ToJson();
            var rpcResult = _rpcServer.Sign(partialJson);

            var contextB = ContractParametersContext.Parse(rpcResult.ToString(), snapshot);
            Assert.IsTrue(contextB.Completed, "Context should be complete after the second signature from RPC.");
            Assert.HasCount(2, contextB.GetSignatures(tx.Sender));
        }
        finally
        {
            _rpcServer.wallet = null;
        }
    }

    [TestMethod]
    public void TestCloseWallet_WhenWalletNotOpen()
    {
        _rpcServer.wallet = null;
        var result = _rpcServer.CloseWallet();
        Assert.IsTrue(result.AsBoolean());
    }

    [TestMethod]
    public void TestDumpPrivKey_WhenWalletNotOpen()
    {
        _rpcServer.wallet = null;
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.DumpPrivKey(new JString(_walletAccount.Address).AsParameter<Address>()),
            "Should throw RpcException for no opened wallet");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestGetNewAddress_WhenWalletNotOpen()
    {
        _rpcServer.wallet = null;
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.GetNewAddress(),
            "Should throw RpcException for no opened wallet");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestGetWalletBalance_WhenWalletNotOpen()
    {
        _rpcServer.wallet = null;
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.GetWalletBalance(NativeContract.NEO.Hash),
            "Should throw RpcException for no opened wallet");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestGetWalletUnclaimedGas_WhenWalletNotOpen()
    {
        _rpcServer.wallet = null;
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.GetWalletUnclaimedGas(),
            "Should throw RpcException for no opened wallet");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestImportPrivKey_WhenWalletNotOpen()
    {
        _rpcServer.wallet = null;
        var privKey = _walletAccount.GetKey().Export();
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.ImportPrivKey(privKey),
            "Should throw RpcException for no opened wallet");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
    }

    [TestMethod]
    public void TestCalculateNetworkFee_InvalidTransactionFormat()
    {
        var invalidTxBase64 = "invalid_base64";
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.CalculateNetworkFee(invalidTxBase64.ToStrictUtf8Bytes()),
            "Should throw RpcException for invalid transaction format");
        Assert.AreEqual(exception.HResult, RpcError.InvalidParams.Code);
    }

    [TestMethod]
    public void TestListAddress_WhenWalletNotOpen()
    {
        // Ensure the wallet is not open
        _rpcServer.wallet = null;

        // Attempt to call ListAddress and expect an RpcException
        var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.ListAddress());

        // Verify the exception has the expected error code
        Assert.AreEqual(RpcError.NoOpenedWallet.Code, exception.HResult);
    }

    [TestMethod]
    [Obsolete]
    public void TestCancelTransaction()
    {
        TestUtilOpenWallet();

        var snapshot = _neoSystem.GetSnapshotCache();
        var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
        snapshot.Commit();

        var address = new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion);
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.CancelTransaction(tx.Hash, [address]),
            "Should throw RpcException for non-existing transaction");
        Assert.AreEqual(RpcError.InsufficientFunds.Code, exception.HResult);

        // Test with invalid transaction id
        var invalidTxHash = "invalid_txid";
        exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.CancelTransaction(new JString(invalidTxHash).AsParameter<UInt256>(), [address]),
            "Should throw RpcException for invalid txid");
        Assert.AreEqual(exception.HResult, RpcError.InvalidParams.Code);

        // Test with no signer
        exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.CancelTransaction(tx.Hash, []),
            "Should throw RpcException for invalid txid");
        Assert.AreEqual(exception.HResult, RpcError.BadRequest.Code);

        // Test with null wallet
        _rpcServer.wallet = null;
        exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.CancelTransaction(tx.Hash, [address]),
            "Should throw RpcException for no opened wallet");
        Assert.AreEqual(exception.HResult, RpcError.NoOpenedWallet.Code);
        TestUtilCloseWallet();

        // Test valid cancel
        _rpcServer.wallet = _wallet;
        var resp = (JObject)_rpcServer.SendFrom(
            NativeContract.GAS.Hash,
            new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion),
            new Address(_walletAccount.ScriptHash, ProtocolSettings.Default.AddressVersion),
            "1"
        );

        var txHash = resp["hash"];
        resp = (JObject)_rpcServer.CancelTransaction(
            txHash.AsParameter<UInt256>(), new JArray(ValidatorAddress).AsParameter<Address[]>(), "1");
        Assert.AreEqual(12, resp.Count);
        Assert.AreEqual(resp["sender"], ValidatorAddress);

        var signers = (JArray)resp["signers"];
        Assert.HasCount(1, signers);
        Assert.AreEqual(signers[0]["account"], ValidatorScriptHash.ToString());
        Assert.AreEqual(nameof(WitnessScope.None), signers[0]["scopes"]);
        Assert.AreEqual(nameof(TransactionAttributeType.Conflicts), resp["attributes"][0]["type"]);
        _rpcServer.wallet = null;
    }

    [TestMethod]
    public void TestInvokeContractVerify()
    {
        var scriptHash = UInt160.Parse("0x70cde1619e405cdef363ab66a1e8dce430d798d5");
        var exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.InvokeContractVerify(scriptHash),
            "Should throw RpcException for unknown contract");
        Assert.AreEqual(exception.HResult, RpcError.UnknownContract.Code);

        // Test with invalid script hash
        exception = Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.InvokeContractVerify(new JString("invalid_script_hash").AsParameter<UInt160>()),
            "Should throw RpcException for invalid script hash");
        Assert.AreEqual(exception.HResult, RpcError.InvalidParams.Code);

        string base64NefFile = "TkVGM05lby5Db21waWxlci5DU2hhcnAgMy43LjQrNjAzNGExODIxY2E3MDk0NjBlYzMxMzZjNzBjMmRjY" +
            "zNiZWEuLi4AAAAAAGNXAAJ5JgQiGEEtUQgwE84MASDbMEGb9mfOQeY/GIRADAEg2zBBm/ZnzkGSXegxStgkCUrKABQoAzpB\u002B" +
            "CfsjEBXAAERiEoQeNBBm/ZnzkGSXegxStgkCUrKABQoAzpB\u002BCfsjEDo2WhC";
        string manifest = """
        {
            "name":"ContractWithVerify",
            "groups":[],
            "features":{},
            "supportedstandards":[],
            "abi":{
                "methods":[
                    {
                        "name":"_deploy",
                        "parameters":[{"name":"data","type":"Any"},{"name":"update","type":"Boolean"}],
                        "returntype":"Void",
                        "offset":0,
                        "safe":false
                    }, {
                        "name":"verify",
                        "parameters":[],
                        "returntype":"Boolean",
                        "offset":31,
                        "safe":false
                    }, {
                        "name":"verify",
                        "parameters":[{"name":"prefix","type":"Integer"}],
                        "returntype":"Boolean",
                        "offset":63,
                        "safe":false
                    }
                ],
                "events":[]
            },
            "permissions":[],
            "trusts":[],
            "extra":{"nef":{"optimization":"All"}}
        }
        """;

        var deployResp = (JObject)_rpcServer.InvokeFunction(
            NativeContract.ContractManagement.Hash,
            "deploy",
            [
                new(ContractParameterType.ByteArray) { Value = Convert.FromBase64String(base64NefFile) },
                new(ContractParameterType.String) { Value = manifest },
            ],
            validatorSigner.AsParameter<SignersAndWitnesses>()
        );
        Assert.AreEqual(nameof(VMState.HALT), deployResp["state"]);

        var deployedScriptHash = new UInt160(Convert.FromBase64String(deployResp["notifications"][0]["state"]["value"][0]["value"].AsString()));
        var snapshot = _neoSystem.GetSnapshotCache();
        var tx = new Transaction
        {
            Nonce = 233, // Restore original nonce
            ValidUntilBlock = NativeContract.Ledger.CurrentIndex(snapshot) + _neoSystem.Settings.MaxValidUntilBlockIncrement,
            Signers = [new Signer() { Account = ValidatorScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = Array.Empty<TransactionAttribute>(),
            Script = Convert.FromBase64String(deployResp["script"].AsString()),
            Witnesses = null,
        };

        var engine = ApplicationEngine.Run(tx.Script, snapshot, container: tx, settings: _neoSystem.Settings, gas: 1200_0000_0000);
        engine.SnapshotCache.Commit();

        // invoke verify without signer; should return false
        var resp = (JObject)_rpcServer.InvokeContractVerify(deployedScriptHash);
        Assert.AreEqual(nameof(VMState.HALT), resp["state"]);
        Assert.IsFalse(resp["stack"][0]["value"].AsBoolean());

        // invoke verify with signer; should return true
        resp = (JObject)_rpcServer.InvokeContractVerify(deployedScriptHash, [], validatorSigner.AsParameter<SignersAndWitnesses>());
        Assert.AreEqual(nameof(VMState.HALT), resp["state"]);
        Assert.IsTrue(resp["stack"][0]["value"].AsBoolean());

        // invoke verify with wrong input value; should FAULT
        resp = (JObject)_rpcServer.InvokeContractVerify(
            deployedScriptHash,
            new JArray([
                new JObject() { ["type"] = nameof(ContractParameterType.Integer), ["value"] = "0" }
            ]).AsParameter<ContractParameter[]>(),
            validatorSigner.AsParameter<SignersAndWitnesses>()
        );
        Assert.AreEqual(nameof(VMState.FAULT), resp["state"]);
        Assert.AreEqual("The argument \u0060hashOrPubkey\u0060 can\u0027t be null.", resp["exception"]);

        // invoke verify with 1 param and signer; should return true
        resp = (JObject)_rpcServer.InvokeContractVerify(
            deployedScriptHash.ToString(),
            new JArray([
                new JObject() { ["type"] = nameof(ContractParameterType.Integer), ["value"] = "32" }
            ]).AsParameter<ContractParameter[]>(),
            validatorSigner.AsParameter<SignersAndWitnesses>()
        );
        Assert.AreEqual(nameof(VMState.HALT), resp["state"]);
        Assert.IsTrue(resp["stack"][0]["value"].AsBoolean());

        // invoke verify with 2 param (which does not exist); should throw Exception
        Assert.ThrowsExactly<RpcException>(
            () => _ = _rpcServer.InvokeContractVerify(
                deployedScriptHash.ToString(),
                new JArray([
                    new JObject() { ["type"] = nameof(ContractParameterType.Integer), ["value"] = "32" },
                    new JObject() { ["type"] = nameof(ContractParameterType.Integer), ["value"] = "32" }
                ]).AsParameter<ContractParameter[]>(),
                validatorSigner.AsParameter<SignersAndWitnesses>()
            ),
            $"Invalid contract verification function - The smart contract {deployedScriptHash} haven't got verify method with 2 input parameters."
        );
    }

    private void TestUtilOpenWallet([CallerMemberName] string callerMemberName = "")
    {
        const string Password = "123456";

        // Avoid using the same wallet file for different tests when they are run in parallel
        var path = $"wallet_{callerMemberName}.json";
        File.WriteAllText(path, WalletJson);

        _rpcServer.OpenWallet(path, Password);
    }

    private void TestUtilCloseWallet()
    {

        const string Path = "wallet-TestUtilCloseWallet.json";
        _rpcServer.CloseWallet();
        File.Delete(Path);
    }
}
