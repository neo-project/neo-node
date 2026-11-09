// Copyright (C) 2015-2026 The Neo Project.
//
// RpcNep11Transfers.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Wallets;
using System.Numerics;

namespace Neo.Network.RPC.Models;

public class RpcNep11Transfers
{
    public UInt160 UserScriptHash { get; set; }

    public List<RpcNep11Transfer> Sent { get; set; }

    public List<RpcNep11Transfer> Received { get; set; }

    public JObject ToJson(ProtocolSettings protocolSettings)
    {
        return new()
        {
            ["sent"] = Sent.Select(p => p.ToJson(protocolSettings)).ToArray(),
            ["received"] = Received.Select(p => p.ToJson(protocolSettings)).ToArray(),
            ["address"] = UserScriptHash.ToAddress(protocolSettings.AddressVersion)
        };
    }

    public static RpcNep11Transfers FromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new()
        {
            Sent = ((JArray)json["sent"]).Select(p => RpcNep11Transfer.FromJson((JObject)p, protocolSettings)).ToList(),
            Received = ((JArray)json["received"]).Select(p => RpcNep11Transfer.FromJson((JObject)p, protocolSettings)).ToList(),
            UserScriptHash = json["address"].ToScriptHash(protocolSettings)
        };
    }
}

public class RpcNep11Transfer
{
    public ulong TimestampMS { get; set; }

    public UInt160 AssetHash { get; set; }

    public UInt160 UserScriptHash { get; set; }

    public BigInteger Amount { get; set; }

    public uint BlockIndex { get; set; }

    public ushort TransferNotifyIndex { get; set; }

    public UInt256 TxHash { get; set; }

    public string TokenId { get; set; }

    public JObject ToJson(ProtocolSettings protocolSettings)
    {
        return new()
        {
            ["timestamp"] = TimestampMS,
            ["assethash"] = AssetHash.ToString(),
            ["transferaddress"] = UserScriptHash?.ToAddress(protocolSettings.AddressVersion),
            ["amount"] = Amount.ToString(),
            ["blockindex"] = BlockIndex,
            ["transfernotifyindex"] = TransferNotifyIndex,
            ["txhash"] = TxHash.ToString(),
            ["tokenid"] = TokenId
        };
    }

    public static RpcNep11Transfer FromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new RpcNep11Transfer
        {
            TimestampMS = (ulong)json["timestamp"].AsNumber(),
            AssetHash = json["assethash"].ToScriptHash(protocolSettings),
            UserScriptHash = json["transferaddress"]?.ToScriptHash(protocolSettings),
            Amount = BigInteger.Parse(json["amount"].AsString()),
            BlockIndex = (uint)json["blockindex"].AsNumber(),
            TransferNotifyIndex = (ushort)json["transfernotifyindex"].AsNumber(),
            TxHash = UInt256.Parse(json["txhash"].AsString()),
            TokenId = json["tokenid"].AsString()
        };
    }
}
