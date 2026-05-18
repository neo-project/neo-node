// Copyright (C) 2015-2026 The Neo Project.
//
// DeferredRelayActor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.Plugins.DeferredRelay;

internal sealed class DeferredRelayActor : UntypedActor
{
    private readonly NeoSystem _neo;
    private readonly IStore _store;
    private readonly DeferredRelaySettings _settings;

    public DeferredRelayActor(NeoSystem neo, IStore store, DeferredRelaySettings settings)
    {
        _neo = neo;
        _store = store;
        _settings = settings;
    }

    protected override void PreStart()
    {
        Context.System.EventStream.Subscribe(Self, typeof(Blockchain.RelayResult));
        Context.System.EventStream.Subscribe(Self, typeof(Blockchain.PersistCompleted));
    }

    protected override void PostStop() =>
        Context.System.EventStream.Unsubscribe(Self);

    protected override void OnReceive(object message)
    {
        switch (message)
        {
            case Blockchain.RelayResult { Inventory: Transaction tx, Result: VerifyResult.NotYetValid } rr:
                DeferredRelayEngine.TryOffer(_neo, _store, _settings, tx, rr.Result);
                break;
            case Blockchain.PersistCompleted pc:
                DeferredRelayEngine.OnPersistCompleted(_neo, _store, _settings, pc.Block);
                break;
        }
    }
}
