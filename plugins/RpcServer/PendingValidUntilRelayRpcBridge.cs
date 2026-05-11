// Copyright (C) 2015-2026 The Neo Project.
//
// PendingValidUntilRelayRpcBridge.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System.IO;
using System.Reflection;

namespace Neo.Plugins.RpcServer;

/// <summary>
/// RpcServer does not reference neo-cli; the pending host registered by the CLI is resolved at runtime by type name.
/// <see cref="TryOffer"/> must stay consistent with <c>Neo.CLI.PendingValidUntilRelay.TryOffer</c>.
/// </summary>
internal static class PendingValidUntilRelayRpcBridge
{
    /// <summary><c>error.data</c> for <see cref="VerifyResult.Expired"/> when the tx was stored for local deferred relay (keep in sync with CLI behavior).</summary>
    internal const string RpcExpiredDataWhenQueuedLocally = "queued_locally";

    private const string HostTypeName = "Neo.CLI.PendingValidUntilRelayHost";
    private const string HostAssemblyName = "neo-cli";

    private static readonly Lazy<Type?> s_hostType = new(ResolveHostType);
    private static readonly Lazy<MethodInfo?> s_getServiceOpen = new(ResolveGetServiceOpen);
    private static readonly Lazy<MethodInfo?> s_getPendingState = new(ResolveGetPendingState);

    private static Type? ResolveHostType()
    {
        var t = Type.GetType($"{HostTypeName}, {HostAssemblyName}");
        if (t is not null) return t;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(asm.GetName().Name, HostAssemblyName, StringComparison.Ordinal))
            {
                t = asm.GetType(HostTypeName);
                if (t is not null) return t;
            }
        }
        try
        {
            return Assembly.Load(HostAssemblyName).GetType(HostTypeName);
        }
        catch
        {
            return null;
        }
    }

    private static MethodInfo? ResolveGetServiceOpen()
    {
        return typeof(NeoSystem).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(m => m.Name == nameof(NeoSystem.GetService) && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1);
    }

    private static MethodInfo? ResolveGetPendingState()
    {
        Type? hostType = s_hostType.Value;
        if (hostType is null) return null;
        Type? pendingType = hostType.Assembly.GetType("Neo.CLI.PendingValidUntilRelay");
        return pendingType?.GetMethod("GetPendingState", BindingFlags.Public | BindingFlags.Static, null,
            [typeof(NeoSystem), hostType], null);
    }

    private static object? TryGetHost(NeoSystem system)
    {
        Type? hostType = s_hostType.Value;
        MethodInfo? open = s_getServiceOpen.Value;
        if (hostType is null || open is null) return null;
        MethodInfo closed = open.MakeGenericMethod(hostType);
        return closed.Invoke(system, [null]);
    }

    private static bool IsFarFutureValidityWindow(Transaction tx, NeoSystem system)
    {
        var snapshot = system.StoreView;
        if (!NativeContract.Ledger.ContainsBlock(snapshot, system.GenesisBlock.Hash))
            return false;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        uint maxInc = snapshot.GetMaxValidUntilBlockIncrement(system.Settings);
        return tx.ValidUntilBlock > height + maxInc;
    }

    /// <summary>
    /// Same rules as <c>Neo.CLI.PendingValidUntilRelay.TryOffer</c> (including <c>PendingRelayMaxTransactions</c> on the host, which neo-cli loads from <c>config.json</c> <c>ApplicationConfiguration</c>→<c>P2P</c>); keep in sync when that method changes.
    /// </summary>
    internal static bool TryOffer(NeoSystem system, Transaction tx, VerifyResult relayResult)
    {
        object? host = TryGetHost(system);
        if (host is null) return false;

        Type hostType = host.GetType();
        PropertyInfo? cfgProp = hostType.GetProperty("Configuration", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? storeProp = hostType.GetProperty("Store", BindingFlags.Public | BindingFlags.Instance);
        if (cfgProp is null || storeProp is null) return false;

        object? cfg = cfgProp.GetValue(host);
        if (cfg is null) return false;
        Type cfgType = cfg.GetType();
        PropertyInfo? freqProp = cfgType.GetProperty("PendingCheckFrequency", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? maxProp = cfgType.GetProperty("PendingRelayMaxTransactions", BindingFlags.Public | BindingFlags.Instance);
        if (freqProp is null || maxProp is null) return false;

        uint pendingRelayMaxTransactions = Convert.ToUInt32(maxProp.GetValue(cfg) ?? 0u);
        uint pendingCheckFrequency = Convert.ToUInt32(freqProp.GetValue(cfg) ?? 0u);
        if (pendingRelayMaxTransactions == 0 || pendingCheckFrequency == 0)
            return false;
        if (relayResult != VerifyResult.Expired)
            return false;
        if (!IsFarFutureValidityWindow(tx, system))
            return false;

        UInt256 hash = tx.Hash;
        if (system.ContainsTransaction(hash) != ContainsTransactionType.NotExist)
            return false;

        if (storeProp.GetValue(host) is not IStore store)
            return false;

        byte[] key = hash.GetSpan().ToArray();
        if (store.Contains(key))
            return true;

        int count = 0;
        foreach ((byte[] k, _) in store.Find())
        {
            if (k.Length == UInt256.Length) count++;
        }
        if (count >= pendingRelayMaxTransactions) return false;

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            ((ISerializable)tx).Serialize(writer);
        }
        store.Put(key, ms.ToArray());
        return true;
    }

    /// <summary>Delegates to <c>Neo.CLI.PendingValidUntilRelay.GetPendingState</c> when neo-cli is loaded.</summary>
    internal static JToken GetPendingState(NeoSystem system)
    {
        MethodInfo? m = s_getPendingState.Value;
        Type? hostType = s_hostType.Value;
        if (m is null || hostType is null)
        {
            return new JObject
            {
                ["enabled"] = false,
                ["pending"] = new JArray(),
                ["count"] = 0,
                ["unavailable"] = true,
            };
        }

        object? host = TryGetHost(system);
        object? r = m.Invoke(null, [system, host]);
        if (r is JObject jo) return jo;
        if (r is JToken jt) return jt;
        return new JObject { ["enabled"] = false, ["pending"] = new JArray(), ["count"] = 0 };
    }
}
