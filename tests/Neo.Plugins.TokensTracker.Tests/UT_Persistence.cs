// Copyright (C) 2015-2026 The Neo Project.
//
// UT_Persistence.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.Trackers;
using Neo.Plugins.Trackers.NEP_11;
using Neo.Plugins.Trackers.NEP_17;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Reflection;

namespace Neo.Plugins.TokensTracker.Tests;

[TestClass]
public class UT_Persistence
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MalformedTransferDoesNotPreventLaterBalanceAndHistory(bool nft)
    {
        var settings = ProtocolSettings.Default with
        {
            StandbyCommittee = System.Collections.Immutable.ImmutableArray.Create(Neo.Cryptography.ECC.ECCurve.Secp256r1.G),
            ValidatorsCount = 1
        };
        using var system = new NeoSystem(settings, new MemoryProvider());
        using var snapshot = system.GetSnapshotCache();
        using var database = new MemoryStore();
        var recipient = UInt160.Parse("0x0000000000000000000000000000000000000001");
        var asset = UInt160.Parse("0x0000000000000000000000000000000000000002");
        var token = Enumerable.Repeat((byte)0xab, 64).ToArray();
        using var balanceScript = new ScriptBuilder();
        balanceScript.Emit(OpCode.INITSLOT, new byte[] { 0, 1 });
        balanceScript.EmitPush(500);
        balanceScript.Emit(OpCode.RET);
        var contract = new ContractState
        {
            Id = 123,
            Hash = asset,
            Nef = new NefFile { Compiler = "test", Source = "", Tokens = [], Script = balanceScript.ToArray() },
            Manifest = new ContractManifest
            {
                Name = "TransferFixture",
                Groups = [],
                SupportedStandards = [nft ? "NEP-11" : "NEP-17"],
                Permissions = [ContractPermission.DefaultPermission],
                Trusts = WildcardContainer<ContractPermissionDescriptor>.Create(),
                Abi = new ContractAbi
                {
                    Methods = [new ContractMethodDescriptor { Name = "balanceOf", Offset = 0, Safe = true,
                        Parameters = [new ContractParameterDefinition { Name = "account", Type = ContractParameterType.Hash160 }],
                        ReturnType = ContractParameterType.Integer }],
                    Events = [new ContractEventDescriptor { Name = "Transfer", Parameters =
                        Enumerable.Range(0, nft ? 4 : 3).Select(i => new ContractParameterDefinition
                            { Name = "arg" + i, Type = ContractParameterType.Any }).ToArray() }]
                }
            }
        };
        contract.Nef.CheckSum = NefFile.ComputeChecksum(contract.Nef);
        snapshot.Add(new KeyBuilder(NativeContract.ContractManagement.Id, 8).Add(asset), new StorageItem(contract));
        var tx = new Transaction { Script = new byte[] { (byte)OpCode.RET }, Signers = [], Attributes = [], Witnesses = [] };
        using var script = new ScriptBuilder();
        foreach (bool malformed in new[] { true, false })
        {
            if (nft) script.EmitPush(token);
            if (malformed) script.EmitPush(new byte[33]);
            else script.EmitPush(nft ? 1 : 500);
            script.EmitPush(recipient.ToArray());
            script.Emit(OpCode.PUSHNULL);
            script.EmitPush(nft ? 4 : 3);
            script.Emit(OpCode.PACK);
            script.EmitPush("Transfer");
            script.EmitSysCall(ApplicationEngine.System_Runtime_Notify);
        }
        using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshot, system.GenesisBlock,
            settings: system.Settings, gas: 10_00000000);
        engine.LoadScript(script.ToArray(), configureState: state =>
        {
            state.Contract = contract;
            state.ScriptHash = asset;
        });
        Assert.AreEqual(VMState.HALT, engine.Execute(), engine.FaultException?.ToString());
        Assert.HasCount(2, engine.Notifications);
        // Core exposes execution events to plugins, but its event constructor is internal.
        var execution = (Blockchain.ApplicationExecuted)Activator.CreateInstance(typeof(Blockchain.ApplicationExecuted),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { engine }, null)!;
        using TrackerBase tracker = nft
            ? new Nep11Tracker(database, 100, true, system)
            : new Nep17Tracker(database, 100, true, system);
        var block = new Block { Header = new Header { Index = 1, Timestamp = 1, PrevHash = UInt256.Zero, MerkleRoot = UInt256.Zero, NextConsensus = UInt160.Zero, Witness = new Witness { InvocationScript = ReadOnlyMemory<byte>.Empty, VerificationScript = ReadOnlyMemory<byte>.Empty } }, Transactions = [tx] };
        tracker.ResetBatch();
        tracker.OnPersist(system, block, snapshot, [execution]);
        tracker.Commit();

        if (nft)
        {
            var balance = database.FindPrefix<Nep11BalanceKey, TokenBalance>(new byte[] { 0xf8 }).Single();
            Assert.AreEqual(System.Numerics.BigInteger.One, balance.Item2.Balance);
            CollectionAssert.AreEqual(token, balance.Item1.Token.GetSpan().ToArray());
            Assert.HasCount(1, tracker.QueryTransfers<Nep11TransferKey, TokenTransfer>(0xfa, recipient, 0, 2));
        }
        else
        {
            var balance = database.FindPrefix<Nep17BalanceKey, TokenBalance>(new byte[] { 0xe8 }).Single();
            Assert.AreEqual(new System.Numerics.BigInteger(500), balance.Item2.Balance);
            Assert.HasCount(1, tracker.QueryTransfers<Nep17TransferKey, TokenTransfer>(0xea, recipient, 0, 2));
        }
    }

    private sealed class MemoryProvider : IStoreProvider
    {
        public string Name => "TokensTrackerTest";
        public IStore GetStore(string? path) => new MemoryStore();
    }
}
