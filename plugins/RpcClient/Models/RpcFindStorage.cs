// Copyright (C) 2015-2026 The Neo Project.
//
// RpcFindStorage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.Network.RPC.Models;

public class RpcFindStorage
{
    public bool Truncated { get; set; }

    public int Next { get; set; }

    public List<RpcStorageKeyValue> Results { get; set; }

    public JObject ToJson()
    {
        return new()
        {
            ["truncated"] = Truncated,
            ["next"] = Next,
            ["results"] = Results.Select(p => p.ToJson()).ToArray()
        };
    }

    public static RpcFindStorage FromJson(JObject json)
    {
        return new()
        {
            Truncated = json["truncated"].AsBoolean(),
            Next = (int)json["next"].AsNumber(),
            Results = ((JArray)json["results"]).Select(p => RpcStorageKeyValue.FromJson((JObject)p)).ToList()
        };
    }
}

public class RpcStorageKeyValue
{
    public string Key { get; set; }

    public string Value { get; set; }

    public JObject ToJson() => new() { ["key"] = Key, ["value"] = Value };

    public static RpcStorageKeyValue FromJson(JObject json)
    {
        return new()
        {
            Key = json["key"].AsString(),
            Value = json["value"].AsString()
        };
    }
}
