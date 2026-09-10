// Copyright (C) 2015-2026 The Neo Project.
//
// RpcCandidate.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System.Numerics;

namespace Neo.Network.RPC.Models;

public class RpcCandidate
{
    public string PublicKey { get; set; }

    public BigInteger Votes { get; set; }

    public bool Active { get; set; }

    public JObject ToJson() => new()
    {
        ["publickey"] = PublicKey,
        ["votes"] = Votes.ToString(),
        ["active"] = Active
    };

    public static RpcCandidate FromJson(JObject json)
    {
        return new RpcCandidate
        {
            PublicKey = json["publickey"].AsString(),
            Votes = BigInteger.Parse(json["votes"].AsString()),
            Active = json["active"].AsBoolean()
        };
    }
}
