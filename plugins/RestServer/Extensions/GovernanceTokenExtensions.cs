// Copyright (C) 2015-2026 The Neo Project.
//
// GovernanceTokenExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions.SmartContract;
using Neo.Persistence;
using Neo.Plugins.RestServer.Models.Blockchain;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;

namespace Neo.Plugins.RestServer.Extensions;
internal static class GovernanceTokenExtensions
{
    private const byte Prefix_AccountState = 12;

    public static IEnumerable<AccountDetails> ListAccounts(this NeoToken neoToken, DataCache snapshot, ProtocolSettings protocolSettings) =>
    neoToken
        .GetAccounts(snapshot)
            .Select(s =>
                new AccountDetails
                {
                    ScriptHash = s.Address,
                    Address = s.Address.ToAddress(protocolSettings.AddressVersion),
                    Balance = s.Balance,
                    Decimals = neoToken.Decimals,
                });
    //public static IEnumerable<AccountDetails> ListNeoAccounts(
    //    this Governance governance,
    //    DataCache snapshot,
    //    ProtocolSettings protocolSettings)
    //    => ListAccountsCore(
    //        snapshot,
    //        protocolSettings,
    //        assetId: governance.NeoTokenId,
    //        decimals: governance.NeoTokenDecimals);

    public static IEnumerable<AccountDetails> ListGasAccounts(
        this Governance governance,
        DataCache snapshot,
        ProtocolSettings protocolSettings)
        => ListAccountsCore(
            snapshot,
            protocolSettings,
            assetId: governance.GasTokenId,
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
