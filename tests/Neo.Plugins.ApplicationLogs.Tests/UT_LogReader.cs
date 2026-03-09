// Copyright (C) 2015-2026 The Neo Project.
//
// UT_LogReader.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Cryptography;
using Neo.Extensions;
using Neo.Extensions.IO;
using Neo.Extensions.VM;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.ApplicationLogs;
using Neo.Plugins.ApplicationLogs.Store.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Serilog;
using System.Numerics;
using ApplicationLogsSettings = Neo.Plugins.ApplicationLogs.ApplicationLogsSettings;

namespace Neo.Plugins.ApplicationsLogs.Tests;

[TestClass]
public class UT_LogReader
{
    static readonly string NeoTransferScript = "CxEMFPlu76Cuc\u002BbgteStE4ozsOWTNUdrDBQtYNweHko3YcnMFOes3ceblcI/lRTAHwwIdHJhbnNmZXIMFPVj6kC8KD1NDgXEjqMFs/Kgc0DvQWJ9W1I=";
    static readonly byte[] ValidatorScript = Contract.CreateSignatureRedeemScript(TestProtocolSettings.SoleNode.StandbyCommittee[0]);
    static readonly UInt160 ValidatorScriptHash = ValidatorScript.ToScriptHash();

    static readonly byte[] MultisigScript = Contract.CreateMultiSigRedeemScript(1, TestProtocolSettings.SoleNode.StandbyCommittee);
    static readonly UInt160 MultisigScriptHash = MultisigScript.ToScriptHash();

    public class TestMemoryStoreProvider(MemoryStore memoryStore) : IStoreProvider
    {
        public MemoryStore MemoryStore { get; init; } = memoryStore;
        public string Name => nameof(MemoryStore);
        public IStore GetStore(string? path) => MemoryStore;
    }

    private class NeoSystemFixture : IDisposable
    {
        public NeoSystem _neoSystem;
        public TestMemoryStoreProvider _memoryStoreProvider;
        public MemoryStore _memoryStore;
        public readonly NEP6Wallet _wallet = TestUtils.GenerateTestWallet("123");
        public WalletAccount _walletAccount;
        public Transaction[] txs;
        public Block block;
        public LogReader logReader;

        public static byte[] CreateNeoTransferScript()
        {
            return NativeContract.Governance.NeoTokenId.MakeScript(
                "transfer",
                MultisigScriptHash,
                ValidatorScriptHash,
                new BigInteger(1),
                null);
        }

        public NeoSystemFixture()
        {
            _memoryStore = new MemoryStore();
            _memoryStoreProvider = new TestMemoryStoreProvider(_memoryStore);
            logReader = new LogReader();
            Plugin.Plugins.Add(logReader);  // initialize before NeoSystem to let NeoSystem load the plugin
            _neoSystem = new NeoSystem(TestProtocolSettings.SoleNode with { Network = ApplicationLogsSettings.Default.Network }, _memoryStoreProvider);
            _walletAccount = _wallet.Import("KxuRSsHgJMb3AMSN6B9P3JHNGMFtxmuimqgR9MmXPcv3CLLfusTd");

            NeoSystem system = _neoSystem;
            txs = [
                new Transaction
                {
                    Nonce = 233,
                    ValidUntilBlock = NativeContract.Ledger.CurrentIndex(system.GetSnapshotCache()) + system.Settings.MaxValidUntilBlockIncrement,
                    Signers = [new Signer() { Account = MultisigScriptHash, Scopes = WitnessScope.CalledByEntry }],
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Script = CreateNeoTransferScript(), //Convert.FromBase64String(NeoTransferScript),
                    NetworkFee = 1000_0000,
                    SystemFee = 1000_0000,
                    Witnesses = []
                }
            ];
            byte[] signature = txs[0].Sign(_walletAccount.GetKey()!, ApplicationLogsSettings.Default.Network);
            txs[0].Witnesses = [new Witness
            {
                InvocationScript = new byte[] { (byte)OpCode.PUSHDATA1, (byte)signature.Length }.Concat(signature).ToArray(),
                VerificationScript = MultisigScript,
            }];
            block = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = _neoSystem.GenesisBlock.Hash,
                    MerkleRoot = new UInt256(),
                    Timestamp = _neoSystem.GenesisBlock.Timestamp + 15_000,
                    Index = 1,
                    NextConsensus = _neoSystem.GenesisBlock.NextConsensus,
                    Witness = null!
                },
                Transactions = txs,
            };
            block.Header.MerkleRoot ??= MerkleTree.ComputeRoot(block.Transactions.Select(t => t.Hash).ToArray());
            signature = block.Sign(_walletAccount.GetKey()!, ApplicationLogsSettings.Default.Network);
            block.Header.Witness = new Witness
            {
                InvocationScript = new byte[] { (byte)OpCode.PUSHDATA1, (byte)signature.Length }.Concat(signature).ToArray(),
                VerificationScript = MultisigScript,
            };
        }

        public void Dispose()
        {
            logReader.Dispose();
            _neoSystem.Dispose();
            _memoryStore.Dispose();
        }
    }

    private static NeoSystemFixture s_neoSystemFixture = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        s_neoSystemFixture = new NeoSystemFixture();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_neoSystemFixture.Dispose();
    }

    [TestMethod]
    public async Task Test_GetApplicationLog()
    {
        NeoSystem system = s_neoSystemFixture._neoSystem;
        Block block = s_neoSystemFixture.block;
        await system.Blockchain.Ask(block, cancellationToken: CancellationToken.None);  // persist the block

        var neoTokenId = NativeContract.Governance.NeoTokenId;
        var gasTokenId = NativeContract.Governance.GasTokenId;
        var tokenContractHash = NativeContract.TokenManagement.Hash;

        JObject blockJson = (JObject)s_neoSystemFixture.logReader.GetApplicationLog(block.Hash);
        Assert.AreEqual(block.Hash.ToString(), blockJson["blockhash"]);

        JArray executions = (JArray)blockJson["executions"]!;
        Assert.HasCount(2, executions);
        Assert.AreEqual("OnPersist", executions[0]!["trigger"]);
        Assert.AreEqual("PostPersist", executions[1]!["trigger"]);

        JArray notifications = (JArray)executions[1]!["notifications"]!;
        Assert.HasCount(1, notifications);
        Assert.AreEqual(tokenContractHash.ToString(), notifications[0]!["contract"]);
        Assert.AreEqual("Transfer", notifications[0]!["eventname"]);  // from null to Validator
        Assert.AreEqual(nameof(ContractParameterType.Any), notifications[0]!["state"]!["value"]![1]!["type"]);
        CollectionAssert.AreEqual(
            ValidatorScriptHash.ToArray(),
            Convert.FromBase64String(notifications[0]!["state"]!["value"]![2]!["value"]!.AsString()));
        Assert.AreEqual("50000000", notifications[0]!["state"]!["value"]![3]!["value"]);

        blockJson = (JObject)s_neoSystemFixture.logReader.GetApplicationLog(block.Hash, "PostPersist");
        executions = (JArray)blockJson["executions"]!;
        Assert.HasCount(1, executions);
        Assert.AreEqual("PostPersist", executions[0]!["trigger"]);

        // "true" is invalid but still works
        JObject transactionJson = (JObject)s_neoSystemFixture.logReader.GetApplicationLog(
            s_neoSystemFixture.txs[0].Hash.ToString(),
            "true");

        executions = (JArray)transactionJson["executions"]!;
        Assert.HasCount(1, executions);
        Assert.AreEqual(nameof(VMState.HALT), executions[0]!["vmstate"]);
        Assert.IsTrue(executions[0]!["stack"]![0]!["value"]!.GetBoolean());

        notifications = (JArray)executions[0]!["notifications"]!;
        Assert.HasCount(2, notifications);

        Assert.AreEqual("Transfer", notifications[0]!["eventname"]!.AsString());
        Assert.AreEqual(neoTokenId.ToString(), notifications[0]!["contract"]!.AsString());
        Assert.AreEqual("1", notifications[0]!["state"]!["value"]![2]!["value"]);

        Assert.AreEqual("Transfer", notifications[1]!["eventname"]!.AsString());
        Assert.AreEqual(gasTokenId.ToString(), notifications[1]!["contract"]!.AsString());
        Assert.AreEqual("50000000", notifications[1]!["state"]!["value"]![3]!["value"]);
    }

    [TestMethod]
    public async Task Test_Commands()
    {
        NeoSystem system = s_neoSystemFixture._neoSystem;
        Block block = s_neoSystemFixture.block;
        await system.Blockchain.Ask(block, cancellationToken: CancellationToken.None);  // persist the block

        var tokenContractHash = NativeContract.TokenManagement.Hash;
        var gasTokenId = NativeContract.Governance.GasTokenId;

        s_neoSystemFixture.logReader.OnGetBlockCommand("1");
        s_neoSystemFixture.logReader.OnGetBlockCommand(block.Hash.ToString());
        s_neoSystemFixture.logReader.OnGetContractCommand(tokenContractHash);
        s_neoSystemFixture.logReader.OnGetTransactionCommand(s_neoSystemFixture.txs[0].Hash);

        var blockLog = s_neoSystemFixture.logReader._neostore.GetBlockLog(block.Hash, TriggerType.PostPersist)!;
        var transactionLog = s_neoSystemFixture.logReader._neostore.GetTransactionLog(s_neoSystemFixture.txs[0].Hash)!;

        Assert.AreEqual(VMState.HALT, blockLog.VmState);
        Assert.HasCount(1, blockLog.Notifications);

        Assert.AreEqual("Transfer", blockLog.Notifications[0].EventName);
        Assert.AreEqual(tokenContractHash, blockLog.Notifications[0].ScriptHash);
        CollectionAssert.AreEqual(
            gasTokenId.ToArray(),
            blockLog.Notifications[0].State[0].GetSpan().ToArray());
        Assert.AreEqual(50000000, blockLog.Notifications[0].State[3]);

        Assert.AreEqual(VMState.FAULT, transactionLog.VmState);
        Assert.IsEmpty(transactionLog.Notifications);

        List<(BlockchainEventModel eventLog, UInt256 txHash)> governanceLogs = s_neoSystemFixture
            .logReader._neostore.GetContractLog(tokenContractHash, TriggerType.PostPersist)
            .ToList();

        Assert.IsGreaterThanOrEqualTo(1, governanceLogs.Count);

        var gasLog = governanceLogs.First(log =>
            log.eventLog.EventName == "Transfer" &&
            log.eventLog.ScriptHash == tokenContractHash &&
            log.eventLog.State[0].GetSpan().SequenceEqual(gasTokenId.ToArray()) &&
            log.eventLog.State[3].GetInteger() == 50000000);

        Assert.AreEqual("Transfer", gasLog.eventLog.EventName);
        Assert.AreEqual(tokenContractHash, gasLog.eventLog.ScriptHash);
        CollectionAssert.AreEqual(
            gasTokenId.ToArray(),
            gasLog.eventLog.State[0].GetSpan().ToArray());
        Assert.AreEqual(50000000, (int)gasLog.eventLog.State[3].GetInteger());
    }
}
