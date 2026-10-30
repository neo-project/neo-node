// Copyright (C) 2015-2026 The Neo Project.
//
// UT_TokensTracker.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.Plugins.Trackers;
using System.Reflection;

namespace Neo.Plugins.TokensTracker.Tests;

[TestClass]
public class UT_TokensTracker
{
    [TestMethod]
    public void CommittingUsesAnIndependentSnapshotForEachTracker()
    {
        var settings = ProtocolSettings.Default with
        {
            StandbyCommittee = System.Collections.Immutable.ImmutableArray.Create(Neo.Cryptography.ECC.ECCurve.Secp256r1.G),
            ValidatorsCount = 1
        };
        using var system = new NeoSystem(settings, new MemoryProvider());
        using var snapshot = system.GetSnapshotCache();
        using var firstStore = new MemoryStore();
        using var secondStore = new MemoryStore();
        using var first = new RecordingTracker(firstStore);
        using var second = new RecordingTracker(secondStore);
        var pluginType = typeof(TrackerBase).Assembly.GetType("Neo.Plugins.TokensTracker")!;
        using var plugin = (global::Neo.Plugins.Plugin)Activator.CreateInstance(pluginType)!;

        var trackers = (List<TrackerBase>)pluginType
            .GetField("trackers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(plugin)!;
        trackers.Add(first);
        trackers.Add(second);

        pluginType
            .GetMethod("Blockchain_Committing_Handler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, [system, null, snapshot, Array.Empty<Blockchain.ApplicationExecuted>()]);

        Assert.IsNotNull(first.ReceivedSnapshot);
        Assert.IsNotNull(second.ReceivedSnapshot);
        Assert.AreNotSame(snapshot, first.ReceivedSnapshot);
        Assert.AreNotSame(snapshot, second.ReceivedSnapshot);
        Assert.AreNotSame(first.ReceivedSnapshot, second.ReceivedSnapshot);
    }

    private sealed class RecordingTracker(IStore store) : TrackerBase(store, 0, false, null!)
    {
        public DataCache? ReceivedSnapshot { get; private set; }

        public override string TrackName => nameof(RecordingTracker);

        public override void OnPersist(NeoSystem system, Block block, DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            ReceivedSnapshot = snapshot;
        }
    }

    private sealed class MemoryProvider : IStoreProvider
    {
        public string Name => "TokensTrackerHandlerTest";
        public IStore GetStore(string? path) => new MemoryStore();
    }
}
