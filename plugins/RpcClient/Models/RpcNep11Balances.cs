// Copyright (C) 2015-2026 The Neo Project.
//
// RpcNep11Balances.cs file belongs to the neo project and is free
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

public class RpcNep11Balances
{
    public UInt160 UserScriptHash { get; set; }

    public List<RpcNep11Balance> Balances { get; set; }

    public JObject ToJson(ProtocolSettings protocolSettings)
    {
        return new()
        {
            ["balance"] = Balances.Select(p => p.ToJson()).ToArray(),
            ["address"] = UserScriptHash.ToAddress(protocolSettings.AddressVersion)
        };
    }

    public static RpcNep11Balances FromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new()
        {
            Balances = ((JArray)json["balance"]).Select(p => RpcNep11Balance.FromJson((JObject)p, protocolSettings)).ToList(),
            UserScriptHash = json["address"].ToScriptHash(protocolSettings)
        };
    }
}

public class RpcNep11Balance
{
    public UInt160 AssetHash { get; set; }

    public string Name { get; set; }

    public string Symbol { get; set; }

    public byte Decimals { get; set; }

    public List<RpcNep11TokenBalance> Tokens { get; set; }

    public JObject ToJson()
    {
        return new()
        {
            ["assethash"] = AssetHash.ToString(),
            ["name"] = Name,
            ["symbol"] = Symbol,
            ["decimals"] = Decimals.ToString(),
            ["tokens"] = Tokens.Select(p => p.ToJson()).ToArray()
        };
    }

    public static RpcNep11Balance FromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new()
        {
            AssetHash = json["assethash"].ToScriptHash(protocolSettings),
            Name = json["name"].AsString(),
            Symbol = json["symbol"].AsString(),
            Decimals = byte.Parse(json["decimals"].AsString()),
            Tokens = ((JArray)json["tokens"]).Select(p => RpcNep11TokenBalance.FromJson((JObject)p)).ToList()
        };
    }
}

public class RpcNep11TokenBalance
{
    public string TokenId { get; set; }

    public BigInteger Amount { get; set; }

    public uint LastUpdatedBlock { get; set; }

    public JObject ToJson()
    {
        return new()
        {
            ["tokenid"] = TokenId,
            ["amount"] = Amount.ToString(),
            ["lastupdatedblock"] = LastUpdatedBlock
        };
    }

    public static RpcNep11TokenBalance FromJson(JObject json)
    {
        return new()
        {
            TokenId = json["tokenid"].AsString(),
            Amount = BigInteger.Parse(json["amount"].AsString()),
            LastUpdatedBlock = (uint)json["lastupdatedblock"].AsNumber()
        };
    }
}
