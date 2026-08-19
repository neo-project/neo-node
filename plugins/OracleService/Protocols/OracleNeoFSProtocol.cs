// Copyright (C) 2015-2026 The Neo Project.
//
// OracleNeoFSProtocol.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using System.Security.Cryptography;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.Plugins.OracleService.Protocols;

class OracleNeoFSProtocol : IOracleProtocol
{
    private readonly ECDsa privateKey;

    public OracleNeoFSProtocol(Wallet wallet, ECPoint[] oracles)
    {
        byte[] key = oracles.Select(wallet.GetAccount)
            .Where(p => p is not null && p.HasKey && !p.Lock)
            .FirstOrDefault()?
            .GetKey()?
            .PrivateKey ?? throw new InvalidOperationException("No available account found for oracle");
        privateKey = key.LoadPrivateKey();
    }

    public void Configure() { }

    public void Dispose()
    {
        privateKey.Dispose();
    }

    public async Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
    {
        OracleService.PluginLogger?.Information("NeoFS request: {Uri}", uri.AbsoluteUri);
        try
        {
            (OracleResponseCode code, string data) = await GetAsync(uri, "https://st1.t5.fs.neo.org:8082", cancellation);
            OracleService.PluginLogger?.Information("NeoFS result, code: {Code}, data: {Data}", code, data);
            return (code, data);
        }
        catch (Exception e)
        {
            OracleService.PluginLogger?.Information("NeoFS result: error,{ErrorMessage}", e.Message);
            return (OracleResponseCode.Error, null);
        }
    }


    /// <summary>
    /// GetAsync returns neofs object from the provided url.
    /// If Command is not provided, full object is requested.
    /// </summary>
    /// <param name="uri">URI scheme is "neofs:ContainerID/ObjectID/Command/offset|length".</param>
    /// <param name="host">Client host.</param>
    /// <param name="cancellation">Cancellation token object.</param>
    /// <returns>Returns neofs object.</returns>
    private async Task<(OracleResponseCode, string)> GetAsync(Uri uri, string host, CancellationToken cancellation)
    {
        string[] ps = uri.AbsolutePath.Split("/");
        if (ps.Length < 2) throw new FormatException("Invalid neofs url");
        ContainerID containerID = ContainerID.FromString(ps[0]);
        ObjectID objectID = ObjectID.FromString(ps[1]);
        Address objectAddr = new()
        {
            ContainerId = containerID,
            ObjectId = objectID
        };
        using Client client = new(privateKey, host);
        var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        tokenSource.CancelAfter(OracleSettings.Default.NeoFS.Timeout);
        if (ps.Length == 2)
        {
            return GetPayload(client, objectAddr, tokenSource.Token);
        }
        return ps[2] switch
        {
            "header" => (OracleResponseCode.Success, await GetHeaderAsync(client, objectAddr, tokenSource.Token)),
            _ => throw new Exception("invalid command")
        };
    }

    private static (OracleResponseCode, string) GetPayload(Client client, Address addr, CancellationToken cancellation)
    {
        var objReader = client.GetObjectInit(addr, options: new CallOptions { Ttl = 2 }, context: cancellation);
        var obj = objReader.ReadHeader();
        if (obj.PayloadSize > OracleResponse.MaxResultSize)
            return (OracleResponseCode.ResponseTooLarge, "");
        var payload = new byte[obj.PayloadSize];
        int offset = 0;
        while (true)
        {
            if ((ulong)offset > obj.PayloadSize) return (OracleResponseCode.ResponseTooLarge, "");
            (byte[] chunk, bool ok) = objReader.ReadChunk();
            if (!ok) break;
            Array.Copy(chunk, 0, payload, offset, chunk.Length);
            offset += chunk.Length;
        }
        return (OracleResponseCode.Success, payload.ToStrictUtf8String());
    }

    private static async Task<string> GetHeaderAsync(Client client, Address addr, CancellationToken cancellation)
    {
        var obj = await client.GetObjectHeader(addr, options: new CallOptions { Ttl = 2 }, context: cancellation);
        return obj.ToString();
    }
}
