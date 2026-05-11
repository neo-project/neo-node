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

using Neo.Extensions;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.Plugins.RpcServer.Tests;

/// <summary>
/// RpcServer does not reference neo-cli; <see cref="PendingValidUntilRelayRpcBridge"/> loads it at runtime.
/// This assembly intentionally does not reference Neo.CLI so the test output matches a plugin-only host:
/// the bridge cannot resolve <c>Neo.CLI.PendingValidUntilRelayHost</c>, and these tests assert the fallback.
/// Integration with a real host and <c>PendingValidUntilRelay.GetPendingState</c> lives in Neo.CLI.Tests.
/// </summary>
[TestClass]
public class UT_PendingValidUntilRelayRpcBridge
{
    private static NeoSystem CreateSystem() =>
        new(TestProtocolSettings.SoleNode, new TestMemoryStoreProvider(new MemoryStore()));

    private static Transaction CreateRoundtrippableTx(uint validUntilBlock, uint nonce = 1) => new()
    {
        Version = 0,
        Nonce = nonce,
        ValidUntilBlock = validUntilBlock,
        Signers = [new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None }],
        Attributes = [],
        Script = new byte[] { (byte)OpCode.RET },
        Witnesses = [Witness.Empty],
    };

    [TestMethod]
    public void GetPendingState_WhenNeoCliAssemblyNotPresent_ReportsUnavailable()
    {
        var neo = CreateSystem();
        var j = (JObject)PendingValidUntilRelayRpcBridge.GetPendingState(neo);
        Assert.IsTrue(j["unavailable"]!.GetBoolean());
        Assert.IsFalse(j["enabled"]!.GetBoolean());
        Assert.AreEqual(0, j["count"]!.AsNumber());
        Assert.IsInstanceOfType(j["pending"], typeof(JArray));
        Assert.AreEqual(0, ((JArray)j["pending"]!).Count);
    }

    [TestMethod]
    public void TryOffer_WhenNeoCliAssemblyNotPresent_AlwaysReturnsFalse()
    {
        var neo = CreateSystem();
        var snapshot = neo.StoreView;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(neo.Settings);
        var inWindow = CreateRoundtrippableTx(validUntilBlock: height + maxInc);
        var farFuture = CreateRoundtrippableTx(validUntilBlock: height + maxInc + 100, nonce: 2);

        Assert.IsFalse(PendingValidUntilRelayRpcBridge.TryOffer(neo, farFuture, VerifyResult.Expired));
        Assert.IsFalse(PendingValidUntilRelayRpcBridge.TryOffer(neo, farFuture, VerifyResult.Succeed));
        Assert.IsFalse(PendingValidUntilRelayRpcBridge.TryOffer(neo, inWindow, VerifyResult.Expired));
    }
}
