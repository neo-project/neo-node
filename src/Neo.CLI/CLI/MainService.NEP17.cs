// Copyright (C) 2015-2026 The Neo Project.
//
// MainService.NEP17.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM.Types;
using Neo.Wallets;
using Array = System.Array;

namespace Neo.CLI;

partial class MainService
{
    /// <summary>
    /// Process "transfer" command
    /// </summary>
    /// <param name="tokenId">Token Id</param>
    /// <param name="to">To</param>
    /// <param name="amount">Amount</param>
    /// <param name="from">From</param>
    /// <param name="data">Data</param>
    /// <param name="signersAccounts">Signer's accounts</param>
    [ConsoleCommand("transfer", Category = "NEP17 Commands")]
    private void OnTransferCommand(UInt160 tokenId, UInt160 to, decimal amount, UInt160? from = null, string? data = null, UInt160[]? signersAccounts = null)
    {
        var snapshot = NeoSystem.StoreView;
        var descriptor = NativeContract.TokenManagement.GetTokenInfo(snapshot, tokenId);

        if (descriptor == null)
        {
            ConsoleHelper.Error($"No token found for token id {tokenId}");
            return;
        }

        var value = new BigDecimal(amount, descriptor!.Decimals);

        if (NoWallet()) return;

        Transaction tx;
        try
        {
            tx = CurrentWallet!.MakeTransaction(snapshot, new[]
            {
                new TransferOutput
                {
                    AssetId = tokenId,
                    Value = value,
                    ScriptHash = to,
                    Data = data
                }
            }, from: from, cosigners: signersAccounts?.Select(p => new Signer
            {
                // default access for transfers should be valid only for first invocation
                Scopes = WitnessScope.CalledByEntry,
                Account = p
            })
            .ToArray() ?? Array.Empty<Signer>());
        }
        catch (InvalidOperationException e)
        {
            ConsoleHelper.Error(GetExceptionMessage(e));
            return;
        }
        if (!ConsoleHelper.ReadUserInput("Relay tx(no|yes)").IsYes())
        {
            return;
        }
        SignAndSendTx(snapshot, tx);
    }

    /// <summary>
    /// Process "balanceOf" command
    /// </summary>
    /// <param name="tokenId">Token Id</param>
    /// <param name="address">Address</param>
    [ConsoleCommand("balanceOf", Category = "NEP17 Commands")]
    private void OnBalanceOfCommand(UInt160 tokenId, UInt160 address)
    {
        var snapshot = NeoSystem.StoreView;
        var descriptor = NativeContract.TokenManagement.GetTokenInfo(snapshot, tokenId);

        if (descriptor == null)
        {
            ConsoleHelper.Error($"No token found for token id {tokenId}");
            return;
        }

        if (!OnInvokeWithResult(NativeContract.TokenManagement.Hash, "balanceOf", out StackItem balanceResult, null,
            new JArray([new JObject
            {
                ["type"] = "Hash160",
                ["value"] = tokenId.ToString()
            },new JObject
            {
                ["type"] = "Hash160",
                ["value"] = address.ToString()
            }]))) return;

        var balance = new BigDecimal(((PrimitiveType)balanceResult).GetInteger(), descriptor.Decimals);

        Console.WriteLine();
        ConsoleHelper.Info($"{descriptor.Name} balance: ", $"{balance}");
    }

    /// <summary>
    /// Process "name" command
    /// </summary>
    /// <param name="tokenHash">Script hash</param>
    [ConsoleCommand("name", Category = "NEP17 Commands")]
    private void OnNameCommand(UInt160 tokenHash)
    {
        ContractState? contract = NativeContract.ContractManagement.GetContract(NeoSystem.StoreView, tokenHash);
        if (contract == null) Console.WriteLine($"Contract hash not exist: {tokenHash}");
        else ConsoleHelper.Info("Result: ", contract.Manifest.Name);
    }

    /// <summary>
    /// Process "decimals" command
    /// </summary>
    /// <param name="tokenHash">Script hash</param>
    [ConsoleCommand("decimals", Category = "NEP17 Commands")]
    private void OnDecimalsCommand(UInt160 tokenHash)
    {
        if (!OnInvokeWithResult(tokenHash, "decimals", out StackItem result)) return;

        ConsoleHelper.Info("Result: ", $"{((PrimitiveType)result).GetInteger()}");
    }

    /// <summary>
    /// Process "totalSupply" command
    /// </summary>
    /// <param name="tokenId">Token Id</param>
    [ConsoleCommand("totalSupply", Category = "NEP17 Commands")]
    private void OnTotalSupplyCommand(UInt160 tokenId)
    {
        var snapshot = NeoSystem.StoreView;
        var descriptor = NativeContract.TokenManagement.GetTokenInfo(snapshot, tokenId);

        if (descriptor == null)
        {
            ConsoleHelper.Error($"No token found for token id {tokenId}");
            return;
        }
        var totalSupply = new BigDecimal(descriptor.TotalSupply, descriptor.Decimals);

        ConsoleHelper.Info("Result: ", $"{totalSupply}");
    }
}
