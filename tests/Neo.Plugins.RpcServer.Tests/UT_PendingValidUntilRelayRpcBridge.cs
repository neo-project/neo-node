// Copyright (C) 2015-2026 The Neo Project.
//
// UT_PendingValidUntilRelayRpcBridge.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.Plugins.RpcServer.Tests;

[TestClass]
public class UT_PendingValidUntilRelayRpcBridge
{
    [TestMethod]
    public void GetPendingState_ReturnsUnavailableWhenNeoCliHostTypeNotLoaded()
    {
        var neo = new NeoSystem(TestProtocolSettings.SoleNode, new TestMemoryStoreProvider(new MemoryStore()));
        JObject j = (JObject)PendingValidUntilRelayRpcBridge.GetPendingState(neo);
        Assert.IsTrue(j.ContainsProperty("unavailable"));
        Assert.IsTrue(j["unavailable"]!.GetBoolean());
        Assert.IsFalse(j["enabled"]!.GetBoolean());
        Assert.AreEqual(0, j["count"]!.AsNumber());
        Assert.IsInstanceOfType(j["pending"], typeof(JArray));
    }

    [TestMethod]
    public void TryOffer_ReturnsFalse_WhenNeoCliHostNotResolvable()
    {
        var neo = new NeoSystem(TestProtocolSettings.SoleNode, new TestMemoryStoreProvider(new MemoryStore()));
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 1,
            ValidUntilBlock = 9999,
            Signers = [],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        Assert.IsFalse(PendingValidUntilRelayRpcBridge.TryOffer(neo, tx, VerifyResult.Expired));
        Assert.IsFalse(PendingValidUntilRelayRpcBridge.TryOffer(neo, tx, VerifyResult.Succeed));
    }

    [TestMethod]
    public void TryOffer_ReturnsFalse_WhenValidUntilNotBeyondAllowedWindow()
    {
        var neo = new NeoSystem(TestProtocolSettings.SoleNode, new TestMemoryStoreProvider(new MemoryStore()));
        var snapshot = neo.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(neo.Settings);
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 1,
            ValidUntilBlock = height + maxInc,
            Signers = [],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        Assert.IsFalse(PendingValidUntilRelayRpcBridge.TryOffer(neo, tx, VerifyResult.Expired));
    }
}
