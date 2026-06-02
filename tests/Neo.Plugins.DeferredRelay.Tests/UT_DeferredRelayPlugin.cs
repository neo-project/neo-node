// Copyright (C) 2015-2026 The Neo Project.
//
// UT_DeferredRelayPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo.CLI.Tests;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.RpcServer;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Reflection;
using System.Text;

namespace Neo.Plugins.DeferredRelay.Tests;

[TestClass]
public class UT_DeferredRelayPlugin
{
    private static IConfigurationSection BuildSection(string json)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build().GetSection("PluginConfiguration");
    }

    private static void LoadSettings(string json) =>
        DeferredRelaySettings.Load(BuildSection(json));

    private static void InvokeOnSystemLoaded(DeferredRelayPlugin plugin, NeoSystem system) =>
        typeof(DeferredRelayPlugin).GetMethod("OnSystemLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, [system]);

    private static object GetPrivateField(DeferredRelayPlugin plugin, string name) =>
        typeof(DeferredRelayPlugin).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(plugin);

    private DeferredRelayPlugin _plugin;
    private TestBlockchain.TestNeoSystem _system;

    [TestCleanup]
    public void Cleanup()
    {
        if (_plugin != null)
        {
            _plugin.Dispose();
            Plugin.Plugins.Remove(_plugin);
        }
        _system?.Dispose();
    }

    [TestMethod]
    public void Plugin_Metadata()
    {
        _plugin = new DeferredRelayPlugin();
        Assert.AreEqual("DeferredRelay", _plugin.Name);
        StringAssert.Contains(_plugin.Description, "NotYetValid");
        StringAssert.Contains(_plugin.ConfigFile, "DeferredRelay.json");
    }

    [TestMethod]
    public void GetPendingValidUntilRelay_HasRpcMethodAttribute()
    {
        var m = typeof(DeferredRelayPlugin).GetMethod(nameof(DeferredRelayPlugin.GetPendingValidUntilRelay));
        Assert.IsNotNull(m);
        Assert.IsNotNull(m.GetCustomAttribute<RpcMethodAttribute>());
    }

    [TestMethod]
    public void GetPendingValidUntilRelay_ThrowsWhenNeoSystemNotLoaded()
    {
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 10,
            "CheckFrequency": 1
          }
        }
        """);
        Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.GetPendingValidUntilRelay());
    }

    [TestMethod]
    public void OnSystemLoaded_NetworkMismatch_DoesNotBindNeoSystem()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 1,
            "MaxTransactions": 10,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        Assert.IsNull(GetPrivateField(_plugin, "_neoSystem"));
        Assert.IsNull(GetPrivateField(_plugin, "_store"));
    }

    [TestMethod]
    public void OnSystemLoaded_Disabled_BindsNeoSystemWithoutStore()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 0,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        Assert.IsNotNull(GetPrivateField(_plugin, "_neoSystem"));
        Assert.IsNull(GetPrivateField(_plugin, "_store"));
        Assert.IsNull(GetPrivateField(_plugin, "_actor"));
    }

    [TestMethod]
    public void GetPendingValidUntilRelay_WhenDisabled_ReturnsDisabledSnapshot()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 0,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        var json = (JObject)_plugin.GetPendingValidUntilRelay();
        Assert.IsFalse(json["enabled"]!.AsBoolean());
        Assert.IsNull(json["pendingrelay"]);
    }

    [TestMethod]
    public void OnSystemLoaded_Enabled_CreatesStoreAndActor()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 100,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        Assert.IsNotNull(GetPrivateField(_plugin, "_store"));
        Assert.IsNotNull(GetPrivateField(_plugin, "_actor"));
    }

    [TestMethod]
    public void Dispose_StopsActorAndClearsStore()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 100,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        _plugin.Dispose();
        Assert.IsNull(GetPrivateField(_plugin, "_store"));
        Assert.IsNull(GetPrivateField(_plugin, "_actor"));
        Plugin.Plugins.Remove(_plugin);
    }

    [TestMethod]
    public void GetRawPendingTx_HasRpcMethodAttribute()
    {
        var m = typeof(DeferredRelayPlugin).GetMethod(nameof(DeferredRelayPlugin.GetRawPendingTx));
        Assert.IsNotNull(m);
        Assert.IsNotNull(m.GetCustomAttribute<RpcMethodAttribute>());
    }

    [TestMethod]
    public void GetRawPendingTx_ThrowsWhenNeoSystemNotLoaded()
    {
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 10,
            "CheckFrequency": 1
          }
        }
        """);
        Assert.ThrowsExactly<InvalidOperationException>(() => _plugin.GetRawPendingTx(UInt256.Zero));
    }

    [TestMethod]
    public void GetRawPendingTx_WhenDisabled_ThrowsUnknownTransaction()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 0,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        var ex = Assert.ThrowsExactly<RpcException>(() => _plugin.GetRawPendingTx(UInt256.Zero));
        Assert.AreEqual(RpcError.UnknownTransaction.Code, ex.HResult);
    }

    [TestMethod]
    public void GetRawPendingTx_NotQueued_ThrowsUnknownTransaction()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 100,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        var unknown = UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20");
        var ex = Assert.ThrowsExactly<RpcException>(() => _plugin.GetRawPendingTx(unknown));
        Assert.AreEqual(RpcError.UnknownTransaction.Code, ex.HResult);
    }

    [TestMethod]
    public void GetRawPendingTx_VerboseFalse_ReturnsBase64()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 100,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        var (tx, raw) = SeedQueuedTx(_plugin, _system, vubOffset: 1000);

        var result = _plugin.GetRawPendingTx(tx.Hash);
        Assert.AreEqual(Convert.ToBase64String(raw), result.AsString());
    }

    [TestMethod]
    public void GetRawPendingTx_VerboseTrue_ReturnsJsonWithBlocksUntilDeadline()
    {
        _system = TestBlockchain.GetSystem();
        _plugin = new DeferredRelayPlugin();
        LoadSettings("""
        {
          "PluginConfiguration": {
            "Network": 860833102,
            "MaxTransactions": 100,
            "CheckFrequency": 1
          }
        }
        """);
        InvokeOnSystemLoaded(_plugin, _system);

        var (tx, _) = SeedQueuedTx(_plugin, _system, vubOffset: 1000);

        var json = (JObject)_plugin.GetRawPendingTx(tx.Hash, verbose: true);
        Assert.AreEqual(tx.Hash.ToString(), json["hash"]!.AsString());
        Assert.AreEqual((double)tx.ValidUntilBlock, json["validuntilblock"]!.AsNumber(), 0.001);
        Assert.IsNotNull(json["script"]);
        Assert.IsNotNull(json["witnesses"]);
        Assert.IsNotNull(json["blocksuntildeadline"]);
        // No on-chain-only fields.
        Assert.IsNull(json["blockhash"]);
        Assert.IsNull(json["confirmations"]);
        Assert.IsNull(json["blocktime"]);
    }

    /// <summary>
    /// Drops a signed transaction directly into the plugin's underlying store and returns
    /// the tx + the serialized bytes that were persisted. Bypasses TryOffer (which would
    /// run extra checks) so the test can focus on the RPC read path.
    /// </summary>
    private static (Transaction tx, byte[] raw) SeedQueuedTx(DeferredRelayPlugin plugin, NeoSystem system, uint vubOffset)
    {
        var store = (IStore)GetPrivateField(plugin, "_store");
        Assert.IsNotNull(store, "Plugin store must be initialized.");

        var wallet = TestUtils.GenerateTestWallet("pwd");
        var account = wallet.CreateAccount();

        uint height = NativeContract.Ledger.CurrentIndex(system.StoreView);
        var tx = new Transaction
        {
            Version = 0,
            Nonce = 7,
            ValidUntilBlock = height + vubOffset,
            Signers = [new Signer { Account = account.ScriptHash, Scopes = WitnessScope.CalledByEntry }],
            Attributes = [],
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = [],
        };
        var ctx = new ContractParametersContext(system.StoreView, tx, system.Settings.Network);
        Assert.IsTrue(wallet.Sign(ctx));
        tx.Witnesses = ctx.GetWitnesses();

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
            ((ISerializable)tx).Serialize(writer);
        byte[] raw = ms.ToArray();
        store.Put(tx.Hash.GetSpan().ToArray(), raw);
        return (tx, raw);
    }
}
