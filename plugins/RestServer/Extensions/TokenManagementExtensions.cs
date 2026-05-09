// Copyright (C) 2015-2026 The Neo Project.
//
// TokenManagementExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using Neo.Plugins.RestServer.Models.Blockchain;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;

namespace Neo.Plugins.RestServer.Extensions;

internal static class TokenManagementExtensions
{
    private const byte Prefix_AccountState = 12;

    public static IEnumerable<AccountDetails> ListNeoAccounts(
        this TokenManagement tokenManagement,
        DataCache snapshot,
        ProtocolSettings protocolSettings)
        => ListAccountsCore(
            snapshot,
            protocolSettings,
            assetId: NativeContract.Governance.NeoTokenId,
            decimals: Governance.NeoTokenDecimals);

    public static IEnumerable<AccountDetails> ListGasAccounts(
        this TokenManagement tokenManagement,
        DataCache snapshot,
        ProtocolSettings protocolSettings)
        => ListAccountsCore(
            snapshot,
            protocolSettings,
            assetId: NativeContract.Governance.GasTokenId,
            decimals: Governance.GasTokenDecimals);

    private static IEnumerable<AccountDetails> ListAccountsCore(
            DataCache snapshot,
            ProtocolSettings protocolSettings,
            UInt160 assetId,
            byte decimals)
    {
        var prefixKey = new StorageKey
        {
            Id = NativeContract.TokenManagement.Id,
            Key = new[] { Prefix_AccountState }
        };

        foreach (var (key, _) in snapshot.Find(prefixKey))
        {
            var rawKey = key.ToArray();

            if (rawKey.Length != 1 + UInt160.Length + UInt160.Length) continue;
            if (rawKey[0] != Prefix_AccountState) continue;

            var account = new UInt160(rawKey.AsSpan(1, UInt160.Length));
            var storedAssetId = new UInt160(rawKey.AsSpan(1 + UInt160.Length, UInt160.Length));
            if (storedAssetId != assetId) continue;

            var balance = NativeContract.TokenManagement.BalanceOf(snapshot, assetId, account);

            yield return new AccountDetails
            {
                ScriptHash = account,
                Address = account.ToAddress(protocolSettings.AddressVersion),
                Balance = balance,
                Decimals = decimals
            };
        }
    }
}
