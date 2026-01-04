// Copyright (C) 2015-2026 The Neo Project.
//
// DBFTPlugin.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.IEventHandlers;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.DBFTPlugin.Consensus;
using Neo.Sign;
using Neo.Wallets;

namespace Neo.Plugins.DBFTPlugin;

public sealed class DBFTPlugin : Plugin, IMessageReceivedHandler
{
    private IWalletProvider? walletProvider;
    private IActorRef consensus = null!;
    private bool started = false;
    private NeoSystem neoSystem = null!;
    private DbftSettings settings = null!;

    public override string Description => "Consensus plugin with dBFT algorithm.";

    public override string ConfigFile => System.IO.Path.Combine(RootPath, "DBFTPlugin.json");

    protected override UnhandledExceptionPolicy ExceptionPolicy => settings.ExceptionPolicy;

    public DBFTPlugin()
    {
        RemoteNode.MessageReceived += ((IMessageReceivedHandler)this).RemoteNode_MessageReceived_Handler;
    }

    public DBFTPlugin(DbftSettings settings) : this()
    {
        this.settings = settings;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            RemoteNode.MessageReceived -= ((IMessageReceivedHandler)this).RemoteNode_MessageReceived_Handler;
        base.Dispose(disposing);
    }

    protected override void Configure()
    {
        settings ??= new DbftSettings(GetConfiguration());
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        if (system.Settings.Network != settings.Network) return;
        neoSystem = system;
        neoSystem.ServiceAdded += NeoSystem_ServiceAdded_Handler;
    }

    void NeoSystem_ServiceAdded_Handler(object? sender, object service)
    {
        if (service is not IWalletProvider provider) return;
        walletProvider = provider;
        neoSystem.ServiceAdded -= NeoSystem_ServiceAdded_Handler;
        if (settings.AutoStart)
        {
            walletProvider.WalletChanged += IWalletProvider_WalletChanged_Handler;
        }
    }

    void IWalletProvider_WalletChanged_Handler(object? sender, Wallet? wallet)
    {
        if (wallet != null)
        {
            walletProvider!.WalletChanged -= IWalletProvider_WalletChanged_Handler;
            Start(wallet);
        }
    }

    /// <summary>
    /// Starts the consensus service.
    /// If the signer name is provided, it will start with the specified signer.
    /// Otherwise, it will start with the WalletProvider's wallet.
    /// </summary>
    /// <param name="signerName">The name of the signer to use.</param>
    [ConsoleCommand("start consensus", Category = "Consensus", Description = "Start consensus service (dBFT)")]
    private void OnStart(string signerName = "")
    {
        var signer = SignerManager.GetSignerOrDefault(signerName)
            ?? walletProvider?.GetWallet();
        if (signer == null)
        {
            ConsoleHelper.Warning("Please open wallet first!");
            return;
        }
        Start(signer);
    }

    public void Start(ISigner signer)
    {
        if (started) return;
        started = true;
        consensus = neoSystem.ActorSystem.ActorOf(ConsensusService.Props(neoSystem, settings, signer));
        consensus.Tell(new ConsensusService.Start());
    }

    bool IMessageReceivedHandler.RemoteNode_MessageReceived_Handler(NeoSystem system, Message message)
    {
        if (message.Command == MessageCommand.Transaction)
        {
            Transaction tx = (Transaction)message.Payload!;
            if (tx.SystemFee > settings.MaxBlockSystemFee)
                return false;
            consensus?.Tell(tx);
        }
        return true;
    }
}
