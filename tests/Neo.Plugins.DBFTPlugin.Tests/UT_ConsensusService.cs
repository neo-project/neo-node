// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ConsensusService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.MsTest;
using Microsoft.Extensions.Configuration;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.DBFTPlugin.Consensus;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.Plugins.DBFTPlugin.Types;
using Neo.Sign;
using Neo.SmartContract;
using Neo.VM;
using System.Reflection;

namespace Neo.Plugins.DBFTPlugin.Tests;

[TestClass]
public class UT_ConsensusService : TestKit
{
    private NeoSystem neoSystem;
    private TestProbe localNode;
    private TestProbe taskManager;
    private TestProbe blockchain;
    private TestProbe txRouter;
    private MockWallet testWallet;
    private MemoryStore memoryStore;

    [TestInitialize]
    public void Setup()
    {
        // Create test probes for actor dependencies
        localNode = CreateTestProbe("localNode");
        taskManager = CreateTestProbe("taskManager");
        blockchain = CreateTestProbe("blockchain");
        txRouter = CreateTestProbe("txRouter");

        // Create memory store
        memoryStore = new MemoryStore();
        var storeProvider = new MockMemoryStoreProvider(memoryStore);

        // Create NeoSystem with correct constructor
        neoSystem = new NeoSystem(MockProtocolSettings.Default, storeProvider);

        // Setup test wallet
        testWallet = new MockWallet(MockProtocolSettings.Default);
        testWallet.AddAccount(MockProtocolSettings.Default.StandbyValidators[0]);
    }

    [TestCleanup]
    public void Cleanup()
    {
        neoSystem?.Dispose();
        Shutdown();
    }

    private ExtensiblePayload CreateConsensusPayload(ConsensusMessage message)
    {
        return new ExtensiblePayload
        {
            Category = "dBFT",
            ValidBlockStart = 0,
            ValidBlockEnd = 100,
            Sender = Contract.GetBFTAddress(MockProtocolSettings.Default.StandbyValidators),
            Data = message.ToArray(),
            Witness = new Witness
            {
                InvocationScript = ReadOnlyMemory<byte>.Empty,
                VerificationScript = new[] { (byte)OpCode.PUSH1 }
            }
        };
    }

    [TestMethod]
    public void TestConsensusServiceCreation()
    {
        // Arrange
        var settings = MockBlockchain.CreateDefaultSettings();

        // Act
        var consensusService = Sys.ActorOf(ConsensusService.Props(neoSystem, settings, testWallet));

        // Assert
        Assert.IsNotNull(consensusService);

        // Verify the service is responsive and doesn't crash on unknown messages
        consensusService.Tell("unknown_message");
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None);

        // Verify the actor is still alive
        Watch(consensusService);
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None); // Should not receive Terminated message
    }

    [TestMethod]
    public void TestConsensusServiceStart()
    {
        // Arrange
        var settings = MockBlockchain.CreateDefaultSettings();
        var consensusService = Sys.ActorOf(ConsensusService.Props(neoSystem, settings, testWallet));

        // Act
        consensusService.Tell(new ConsensusService.Start());

        // Assert - The service should start without throwing exceptions
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None);
    }

    [TestMethod]
    public void TestConsensusServiceReceivesBlockchainMessages()
    {
        // Arrange
        var settings = MockBlockchain.CreateDefaultSettings();
        var consensusService = Sys.ActorOf(ConsensusService.Props(neoSystem, settings, testWallet));

        // Start the consensus service
        consensusService.Tell(new ConsensusService.Start());

        // Create a test block
        var block = new Block
        {
            Header = new Header
            {
                Index = 1,
                PrimaryIndex = 0,
                Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Nonce = 0,
                NextConsensus = Contract.GetBFTAddress(MockProtocolSettings.Default.StandbyValidators),
                PrevHash = UInt256.Zero,
                MerkleRoot = UInt256.Zero,
                Witness = new Witness
                {
                    InvocationScript = ReadOnlyMemory<byte>.Empty,
                    VerificationScript = new[] { (byte)OpCode.PUSH1 }
                }
            },
            Transactions = Array.Empty<Transaction>()
        };

        // Act
        consensusService.Tell(new Blockchain.PersistCompleted(block));

        // Assert - The service should handle the message without throwing
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None);
    }

    [TestMethod]
    public void TestConsensusServiceHandlesExtensiblePayload()
    {
        // Arrange
        var settings = MockBlockchain.CreateDefaultSettings();
        var consensusService = Sys.ActorOf(ConsensusService.Props(neoSystem, settings, testWallet));

        // Start the consensus service
        consensusService.Tell(new ConsensusService.Start());

        // Create a test extensible payload
        var payload = new ExtensiblePayload
        {
            Category = "dBFT",
            ValidBlockStart = 0,
            ValidBlockEnd = 100,
            Sender = Contract.GetBFTAddress(MockProtocolSettings.Default.StandbyValidators),
            Data = new byte[] { 0x01, 0x02, 0x03 },
            Witness = new Witness
            {
                InvocationScript = ReadOnlyMemory<byte>.Empty,
                VerificationScript = new[] { (byte)OpCode.PUSH1 }
            }
        };

        // Act
        consensusService.Tell(payload);

        // Assert - The service should handle the payload without throwing
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None);
    }

    [TestMethod]
    public void TestConsensusServiceHandlesValidConsensusMessage()
    {
        // Arrange
        var settings = MockBlockchain.CreateDefaultSettings();
        var consensusService = Sys.ActorOf(ConsensusService.Props(neoSystem, settings, testWallet));
        consensusService.Tell(new ConsensusService.Start());

        // Create a valid PrepareRequest message
        var prepareRequest = new PrepareRequest
        {
            Version = 0,
            PrevHash = UInt256.Zero,
            ViewNumber = 0,
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Nonce = 0,
            TransactionHashes = Array.Empty<UInt256>()
        };

        var payload = CreateConsensusPayload(prepareRequest);

        // Act
        consensusService.Tell(payload);

        // Assert - Service should process the message without crashing
        ExpectNoMsg(TimeSpan.FromMilliseconds(200), cancellationToken: CancellationToken.None);

        // Verify the actor is still responsive
        Watch(consensusService);
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None); // Should not receive Terminated message
    }

    [TestMethod]
    public void TestConsensusServiceRejectsInvalidPayload()
    {
        // Arrange
        var settings = MockBlockchain.CreateDefaultSettings();
        var consensusService = Sys.ActorOf(ConsensusService.Props(neoSystem, settings, testWallet));
        consensusService.Tell(new ConsensusService.Start());

        // Create an invalid payload (wrong category)
        var invalidPayload = new ExtensiblePayload
        {
            Category = "InvalidCategory",
            ValidBlockStart = 0,
            ValidBlockEnd = 100,
            Sender = Contract.GetBFTAddress(MockProtocolSettings.Default.StandbyValidators),
            Data = new byte[] { 0x01, 0x02, 0x03 },
            Witness = new Witness
            {
                InvocationScript = ReadOnlyMemory<byte>.Empty,
                VerificationScript = new[] { (byte)OpCode.PUSH1 }
            }
        };

        // Act
        consensusService.Tell(invalidPayload);

        // Assert - Service should ignore invalid payload and remain stable
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None);

        // Verify the actor is still alive and responsive
        Watch(consensusService);
        ExpectNoMsg(TimeSpan.FromMilliseconds(100), cancellationToken: CancellationToken.None);
    }

    [TestMethod]
    public void TestConsensusServiceRoutesConsensusMessagesIntoContextState()
    {
        var settings = MockBlockchain.CreateDefaultSettings();
        var consensusServiceRef = ActorOfAsTestActorRef<ConsensusService>(ConsensusService.Props(neoSystem, settings, testWallet));
        var actor = consensusServiceRef.UnderlyingActor;

        InvokeConsensusMethod(actor, "InitializeConsensus", (byte)0);
        var context = GetConsensusContext(actor);
        var blockIndex = context.Block.Index;
        var viewNumber = context.ViewNumber;
        var primaryIndex = context.Block.PrimaryIndex;
        var validTimestamp = Math.Max(context.PrevHeader.Timestamp + 1, TimeProvider.Current.UtcNow.ToTimestampMS());

        var prepareRequest = new PrepareRequest
        {
            BlockIndex = blockIndex,
            ValidatorIndex = primaryIndex,
            ViewNumber = viewNumber,
            Version = context.Block.Version,
            PrevHash = context.Block.PrevHash,
            Timestamp = validTimestamp,
            Nonce = 7,
            TransactionHashes = Array.Empty<UInt256>()
        };
        var prepareRequestPayload = context.CreatePayload(prepareRequest);

        InvokeConsensusMethod(actor, "OnConsensusPayload", prepareRequestPayload);

        Assert.AreSame(prepareRequestPayload, context.PreparationPayloads[primaryIndex]);
        Assert.IsTrue(context.RequestSentOrReceived);
        Assert.AreEqual(validTimestamp, context.Block.Timestamp);
        Assert.IsEmpty(context.TransactionHashes);

        var responseValidatorIndex = primaryIndex == 0 ? 1 : 0;
        var prepareResponse = new PrepareResponse
        {
            BlockIndex = blockIndex,
            ValidatorIndex = (byte)responseValidatorIndex,
            ViewNumber = viewNumber,
            PreparationHash = prepareRequestPayload.Hash
        };
        var prepareResponsePayload = context.CreatePayload(prepareResponse);

        InvokeConsensusMethod(actor, "OnConsensusPayload", prepareResponsePayload);

        Assert.AreSame(prepareResponsePayload, context.PreparationPayloads[responseValidatorIndex]);

        var rejectedHash = UInt256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var changeView = new ChangeView
        {
            BlockIndex = blockIndex,
            ValidatorIndex = 2,
            ViewNumber = viewNumber,
            Timestamp = validTimestamp + 1,
            Reason = ChangeViewReason.TxInvalid,
            RejectedHashes = [rejectedHash]
        };
        var changeViewPayload = context.CreatePayload(changeView);

        InvokeConsensusMethod(actor, "OnConsensusPayload", changeViewPayload);

        Assert.AreSame(changeViewPayload, context.ChangeViewPayloads[2]);
        CollectionAssert.Contains(context.InvalidTransactions[rejectedHash].ToArray(), context.Validators[2]);

        context.TransactionHashes = null;
        var commit = new Commit
        {
            BlockIndex = blockIndex,
            ValidatorIndex = 3,
            ViewNumber = viewNumber,
            Signature = new byte[64]
        };
        var commitPayload = context.CreatePayload(commit);

        InvokeConsensusMethod(actor, "OnConsensusPayload", commitPayload);

        Assert.AreSame(commitPayload, context.CommitPayloads[3]);

        var recoveryRequest = new RecoveryRequest
        {
            BlockIndex = blockIndex,
            ValidatorIndex = 0,
            ViewNumber = viewNumber,
            Timestamp = validTimestamp + 2
        };
        var recoveryRequestPayload = context.CreatePayload(recoveryRequest);

        var originalMyIndex = context.MyIndex;
        context.MyIndex = -1;
        InvokeConsensusMethod(actor, "OnConsensusPayload", recoveryRequestPayload);
        context.MyIndex = originalMyIndex;

        CollectionAssert.Contains(GetKnownHashes(actor).ToArray(), recoveryRequestPayload.Hash);

        var recoveryMessage = new RecoveryMessage
        {
            BlockIndex = blockIndex,
            ValidatorIndex = 4,
            ViewNumber = viewNumber,
            ChangeViewMessages = new Dictionary<byte, RecoveryMessage.ChangeViewPayloadCompact>(),
            PreparationMessages = new Dictionary<byte, RecoveryMessage.PreparationPayloadCompact>(),
            CommitMessages = new Dictionary<byte, RecoveryMessage.CommitPayloadCompact>()
        };
        var recoveryMessagePayload = context.CreatePayload(recoveryMessage);

        InvokeConsensusMethod(actor, "OnConsensusPayload", recoveryMessagePayload);

        Assert.IsFalse(GetBooleanField(actor, "isRecovering"));
    }

    [TestMethod]
    public void TestConsensusServiceExecutesSendAndCheckPaths()
    {
        var settings = CreateCompatibleSettings();

        var primaryServiceRef = ActorOfAsTestActorRef<ConsensusService>(ConsensusService.Props(neoSystem, settings, new TestSigner()));
        var primaryActor = primaryServiceRef.UnderlyingActor;

        primaryServiceRef.Tell(new ConsensusService.Start());

        Assert.IsTrue(GetBooleanField(primaryActor, "started"));

        var primaryContext = GetConsensusContext(primaryActor);
        primaryContext.MyIndex = primaryContext.Block.PrimaryIndex;

        InvokeConsensusMethod(primaryActor, "SendPrepareRequest");

        var prepareRequestPayload = primaryContext.PreparationPayloads[primaryContext.MyIndex];
        Assert.IsNotNull(prepareRequestPayload);
        Assert.IsNotNull(primaryContext.TransactionHashes);

        InvokeConsensusMethod(primaryActor, "RequestChangeView", ChangeViewReason.Timeout, null);

        Assert.IsNotNull(primaryContext.ChangeViewPayloads[primaryContext.MyIndex]);

        var backupServiceRef = ActorOfAsTestActorRef<ConsensusService>(ConsensusService.Props(neoSystem, settings, new TestSigner()));
        var backupActor = backupServiceRef.UnderlyingActor;

        InvokeConsensusMethod(backupActor, "InitializeConsensus", (byte)0);

        var backupContext = GetConsensusContext(backupActor);
        backupContext.TransactionHashes = Array.Empty<UInt256>();
        backupContext.Transactions = new Dictionary<UInt256, Transaction>();
        backupContext.PreparationPayloads[backupContext.Block.PrimaryIndex] = prepareRequestPayload;

        Assert.IsTrue((bool)InvokeConsensusMethod(backupActor, "CheckPrepareResponse"));
        Assert.IsNotNull(backupContext.PreparationPayloads[backupContext.MyIndex]);

        var extraPreparationValidators = Enumerable.Range(0, backupContext.Validators.Length)
            .Where(index => index != backupContext.Block.PrimaryIndex && index != backupContext.MyIndex)
            .Take(backupContext.M - 2);
        foreach (var validatorIndex in extraPreparationValidators)
        {
            backupContext.PreparationPayloads[validatorIndex] = WithWitness(backupContext.CreatePayload(new PrepareResponse
            {
                BlockIndex = backupContext.Block.Index,
                ValidatorIndex = (byte)validatorIndex,
                ViewNumber = backupContext.ViewNumber,
                PreparationHash = prepareRequestPayload.Hash
            }));
        }

        InvokeConsensusMethod(backupActor, "CheckPreparations");

        Assert.IsNotNull(backupContext.CommitPayloads[backupContext.MyIndex]);

        var extraCommitValidators = Enumerable.Range(0, backupContext.Validators.Length)
            .Where(index => index != backupContext.MyIndex)
            .Take(backupContext.M - 1);
        foreach (var validatorIndex in extraCommitValidators)
        {
            backupContext.CommitPayloads[validatorIndex] = WithWitness(backupContext.CreatePayload(new Commit
            {
                BlockIndex = backupContext.Block.Index,
                ValidatorIndex = (byte)validatorIndex,
                ViewNumber = backupContext.ViewNumber,
                Signature = new byte[64]
            }));
        }

        InvokeConsensusMethod(backupActor, "CheckCommits");

        Assert.IsTrue(backupContext.BlockSent);
    }

    private static ConsensusContext GetConsensusContext(ConsensusService actor)
    {
        return (ConsensusContext)typeof(ConsensusService)
            .GetField("context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(actor)!;
    }

    private static HashSet<UInt256> GetKnownHashes(ConsensusService actor)
    {
        return (HashSet<UInt256>)typeof(ConsensusService)
            .GetField("knownHashes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(actor)!;
    }

    private static bool GetBooleanField(ConsensusService actor, string fieldName)
    {
        return (bool)typeof(ConsensusService)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(actor)!;
    }

    private static object InvokeConsensusMethod(ConsensusService actor, string methodName, params object[] args)
    {
        return typeof(ConsensusService)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(actor, args);
    }

    private static ExtensiblePayload WithWitness(ExtensiblePayload payload)
    {
        payload.Witness ??= new Witness
        {
            InvocationScript = new byte[] { (byte)OpCode.PUSH1 },
            VerificationScript = new byte[] { (byte)OpCode.PUSH1 }
        };
        return payload;
    }

    private DbftSettings CreateCompatibleSettings()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ApplicationConfiguration:DBFTPlugin:RecoveryLogs"] = "ConsensusState",
                ["ApplicationConfiguration:DBFTPlugin:IgnoreRecoveryLogs"] = "false",
                ["ApplicationConfiguration:DBFTPlugin:AutoStart"] = "false",
                ["ApplicationConfiguration:DBFTPlugin:Network"] = neoSystem.Settings.Network.ToString(),
                ["ApplicationConfiguration:DBFTPlugin:MaxBlockSize"] = "262144",
                ["ApplicationConfiguration:DBFTPlugin:MaxBlockSystemFee"] = "150000000000"
            })
            .Build();

        return new DbftSettings(config.GetSection("ApplicationConfiguration:DBFTPlugin"));
    }

    private sealed class TestSigner : ISigner
    {
        private static readonly byte[] SignatureBytes = new byte[64];
        private static readonly byte[] InvocationScript = [(byte)OpCode.PUSH1];

        public bool ContainsSignable(ECPoint publicKey) => true;

        public Witness SignExtensiblePayload(ExtensiblePayload payload, DataCache dataCache, uint network)
        {
            return new Witness
            {
                InvocationScript = InvocationScript,
                VerificationScript = InvocationScript
            };
        }

        public ReadOnlyMemory<byte> SignBlock(Block block, ECPoint publicKey, uint network)
        {
            return SignatureBytes;
        }
    }
}
