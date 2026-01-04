// Copyright (C) 2015-2026 The Neo Project.
//
// TestUtils.Transaction.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Neo.VM;

namespace Neo.Plugins.SignClient.Tests;

public partial class TestUtils
{
    public static Transaction GetTransaction(UInt160 sender)
    {
        return new Transaction
        {
            Script = new[] { (byte)OpCode.PUSH2 },
            Attributes = [],
            Signers =
            [
                new()
                {
                    Account = sender,
                    Scopes = WitnessScope.CalledByEntry,
                    AllowedContracts = [],
                    AllowedGroups = [],
                    Rules = [],
                }
            ],
            Witnesses = [Witness.Empty],
        };
    }
}
