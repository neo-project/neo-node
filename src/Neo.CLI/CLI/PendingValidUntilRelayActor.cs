// Copyright (C) 2015-2026 The Neo Project.
//
// PendingValidUntilRelayActor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.Ledger;

namespace Neo.CLI;

internal sealed class PendingValidUntilRelayActor : UntypedActor
{
    private readonly NeoSystem _neo;
    private readonly PendingValidUntilRelayHost _host;

    public PendingValidUntilRelayActor(NeoSystem neo, PendingValidUntilRelayHost host)
    {
        _neo = neo;
        _host = host;
    }

    protected override void PreStart() =>
        Context.System.EventStream.Subscribe(Self, typeof(Blockchain.PersistCompleted));

    protected override void PostStop() =>
        Context.System.EventStream.Unsubscribe(Self);

    protected override void OnReceive(object message)
    {
        if (message is Blockchain.PersistCompleted pc)
            PendingValidUntilRelay.OnPersistCompleted(_neo, _host, pc.Block);
    }
}
