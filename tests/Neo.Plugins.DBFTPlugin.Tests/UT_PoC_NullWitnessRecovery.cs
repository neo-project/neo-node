// Copyright (C) 2015-2026 The Neo Project.
//
// UT_PoC_NullWitnessRecovery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.TestKit.MsTest;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.DBFTPlugin.Consensus;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.Plugins.DBFTPlugin.Types;
using Neo.Sign;
using System.Reflection;

namespace Neo.Plugins.DBFTPlugin.Tests;

[TestClass]
public class UT_PoC_NullWitnessRecovery : TestKit
{
    private NeoSystem neoSystem;
    private MemoryStore memoryStore;

    [TestInitialize]
    public void Setup()
    {
        memoryStore = new MemoryStore();
        var storeProvider = new MockMemoryStoreProvider(memoryStore);
        neoSystem = new NeoSystem(MockProtocolSettings.Default, storeProvider);
    }

    [TestCleanup]
    public void Cleanup()
    {
        neoSystem?.Dispose();
        Shutdown();
    }

    private static ConsensusContext GetConsensusContext(ConsensusService actor)
    {
        return (ConsensusContext)typeof(ConsensusService)
            .GetField("context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(actor)!;
    }

    private static object InvokeConsensusMethod(ConsensusService actor, string methodName, params object[] args)
    {
        return typeof(ConsensusService)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(actor, args);
    }

    [TestMethod]
    public void SignerFailureDoesNotStoreUnsignedChangeViewOrBreakRecovery()
    {
        // Reproduce the failed signing call from the reported PoC.
        var settings = MockBlockchain.CreateDefaultSettings();
        var signer = new FailingSigner();
        var consensusServiceRef = ActorOfAsTestActorRef<ConsensusService>(
            ConsensusService.Props(neoSystem, settings, signer));
        var actor = consensusServiceRef.UnderlyingActor;

        InvokeConsensusMethod(actor, "InitializeConsensus", (byte)0);
        var context = GetConsensusContext(actor);
        Assert.IsTrue(context.MyIndex >= 0, "test signer must be a validator");

        var payload = context.MakeChangeView(ChangeViewReason.Timeout);
        Assert.IsNull(payload);
        Assert.IsNull(context.ChangeViewPayloads[context.MyIndex]);

        Assert.IsNull(context.MakePrepareRequest());
        Assert.IsNull(context.PreparationPayloads[context.MyIndex]);

        context.Reset((byte)(context.ViewNumber + 1));
        Assert.IsNull(context.LastChangeViewPayloads[context.MyIndex]);

        signer.FailSigning = false;
        var recovery = context.MakeRecoveryMessage();
        Assert.IsNotNull(recovery?.Witness);
        var message = (RecoveryMessage)ConsensusMessage.DeserializeFrom(recovery.Data);
        Assert.AreEqual(0, message.ChangeViewMessages.Count);
    }

    [TestMethod]
    public void RecoverySkipsPreviouslyStoredUnsignedChangeView()
    {
        var settings = MockBlockchain.CreateDefaultSettings();
        var signer = new FailingSigner { FailSigning = false };
        var consensusServiceRef = ActorOfAsTestActorRef<ConsensusService>(
            ConsensusService.Props(neoSystem, settings, signer));
        var actor = consensusServiceRef.UnderlyingActor;

        InvokeConsensusMethod(actor, "InitializeConsensus", (byte)0);
        var context = GetConsensusContext(actor);
        var changeView = context.MakeChangeView(ChangeViewReason.Timeout);
        changeView.Witness = null;
        context.Reset((byte)(context.ViewNumber + 1));

        var recovery = context.MakeRecoveryMessage();
        Assert.IsNotNull(recovery?.Witness);
        var message = (RecoveryMessage)ConsensusMessage.DeserializeFrom(recovery.Data);
        Assert.AreEqual(0, message.ChangeViewMessages.Count);
    }

    [TestMethod]
    public void RemoteSignerFailureDoesNotStopChangeViewCreation()
    {
        var settings = MockBlockchain.CreateDefaultSettings();
        var signer = new FailingSigner { UseSignException = true };
        var consensusServiceRef = ActorOfAsTestActorRef<ConsensusService>(
            ConsensusService.Props(neoSystem, settings, signer));
        var actor = consensusServiceRef.UnderlyingActor;

        InvokeConsensusMethod(actor, "InitializeConsensus", (byte)0);
        var context = GetConsensusContext(actor);

        Assert.IsNull(context.MakeChangeView(ChangeViewReason.Timeout));
        Assert.IsNull(context.ChangeViewPayloads[context.MyIndex]);
    }

    private sealed class FailingSigner : ISigner
    {
        public bool FailSigning { get; set; } = true;
        public bool UseSignException { get; set; }
        public bool ContainsSignable(ECPoint publicKey) => true;

        public Witness SignExtensiblePayload(ExtensiblePayload payload, DataCache dataCache, uint network)
        {
            if (FailSigning)
            {
                if (UseSignException)
                    throw new SignException("simulated remote signer outage");
                throw new InvalidOperationException("simulated incomplete ContractParametersContext");
            }
            return Witness.Empty;
        }

        public ReadOnlyMemory<byte> SignBlock(Block block, ECPoint publicKey, uint network)
        {
            throw new InvalidOperationException("simulated locked account");
        }
    }
}
