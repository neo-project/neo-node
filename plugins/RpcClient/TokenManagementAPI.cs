// Copyright (C) 2015-2026 The Neo Project.
//
// TokenManagementAPI.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System.Numerics;

namespace Neo.Network.RPC;

/// <summary>
/// Call TokenManagement methods with RPC API
/// </summary>
public class TokenManagementAPI : ContractClient
{
    /// <summary>
    /// TokenManagementAPI Constructor
    /// </summary>
    /// <param name="rpcClient">the RPC client to call NEO RPC methods</param>
    public TokenManagementAPI(RpcClient rpcClient) : base(rpcClient) { }

    /// <summary>
    /// Get the balance of an asset for an account.
    /// </summary>
    public async Task<BigInteger> BalanceOfAsync(UInt160 assetId, UInt160 account)
    {
        var result = await TestInvokeAsync(NativeContract.TokenManagement.Hash, "balanceOf", assetId, account).ConfigureAwait(false);
        return result.Stack.Single().GetInteger();
    }

    /// <summary>
    /// Get the balance of an asset for an account.
    /// </summary>
    public Task<BigInteger> BalanceOfAsync(UInt160 assetId, string account)
    {
        UInt160 accountHash = Utility.GetScriptHash(account, rpcClient.protocolSettings);
        return BalanceOfAsync(assetId, accountHash);
    }

    /// <summary>
    /// Get token info by asset id.
    /// </summary>
    public Task<RpcInvokeResult> GetTokenInfoAsync(UInt160 assetId)
    {
        return TestInvokeAsync(NativeContract.TokenManagement.Hash, "getTokenInfo", assetId);
    }

    public Task<RpcInvokeResult> GetTokenInfoAsync(UInt160 owner, string name)
    {
        UInt160 assetId = TokenManagement.GetAssetId(owner, name);
        return GetTokenInfoAsync(assetId);
    }

    public async Task<bool> AssetExistsAsync(UInt160 assetId)
    {
        var result = await GetTokenInfoAsync(assetId).ConfigureAwait(false);
        return result.Stack.Single() is not Null;
    }

    /// <summary>
    /// Get assets of owner.
    /// </summary>
    public Task<RpcInvokeResult> GetAssetsOfOwnerAsync(UInt160 account)
    {
        return TestInvokeAsync(NativeContract.TokenManagement.Hash, "getAssetsOfOwner", account);
    }

    /// <summary>
    /// Get assets of owner.
    /// </summary>
    public Task<RpcInvokeResult> GetAssetsOfOwnerAsync(string account)
    {
        UInt160 accountHash = Utility.GetScriptHash(account, rpcClient.protocolSettings);
        return GetAssetsOfOwnerAsync(accountHash);
    }

    /// <summary>
    /// Compute the asset id from owner and token name.
    /// </summary>
    public UInt160 GetAssetId(UInt160 owner, string name)
    {
        return TokenManagement.GetAssetId(owner, name);
    }

    /// <summary>
    /// Get decimals of token managed by TokenManagement.
    /// </summary>
    /// <param name="assetId">token asset id</param>
    /// <returns></returns>
    public async Task<byte> DecimalsAsync(UInt160 assetId)
    {
        var result = await TestInvokeAsync(
            NativeContract.TokenManagement.Hash,
            "getTokenInfo",
            assetId).ConfigureAwait(false);

        var tokenInfo = result.Stack.Single();

        if (tokenInfo is Neo.VM.Types.Null)
            throw new InvalidOperationException("Token does not exist.");

        // TokenState layout expected: [owner, name, symbol, decimals, totalSupply, maxSupply, type]
        var structItem = (Neo.VM.Types.Struct)tokenInfo;
        return (byte)structItem[3].GetInteger();
    }

    /// <summary>
    /// Create token transfer transaction from single-sig account.
    /// </summary>
    public async Task<Transaction> CreateTransferTxAsync(UInt160 assetId, KeyPair fromKey, UInt160 to, BigInteger amount, object? data = null, bool addAssert = true)
    {
        var sender = Contract.CreateSignatureRedeemScript(fromKey.PublicKey).ToScriptHash();
        Signer[] signers = [new Signer { Scopes = WitnessScope.CalledByEntry, Account = sender }];

        byte[] script = NativeContract.TokenManagement.Hash.MakeScript("transfer", assetId, sender, to, amount, data);
        if (addAssert) script = [.. script, (byte)OpCode.ASSERT];

        TransactionManagerFactory factory = new(rpcClient);
        TransactionManager manager = await factory.MakeTransactionAsync(script, signers).ConfigureAwait(false);

        return await manager
            .AddSignature(fromKey)
            .SignAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Create token transfer transaction from multi-sig account.
    /// </summary>
    public async Task<Transaction> CreateTransferTxAsync(UInt160 assetId, int m, ECPoint[] pubKeys, KeyPair[] fromKeys, UInt160 to, BigInteger amount, object? data = null, bool addAssert = true)
    {
        if (m > fromKeys.Length)
            throw new ArgumentException($"Need at least {m} KeyPairs for signing!");

        var sender = Contract.CreateMultiSigContract(m, pubKeys).ScriptHash;
        Signer[] signers = [new Signer { Scopes = WitnessScope.CalledByEntry, Account = sender }];

        byte[] script = NativeContract.TokenManagement.Hash.MakeScript("transfer", assetId, sender, to, amount, data);
        if (addAssert) script = [.. script, (byte)OpCode.ASSERT];

        TransactionManagerFactory factory = new(rpcClient);
        TransactionManager manager = await factory.MakeTransactionAsync(script, signers).ConfigureAwait(false);

        return await manager
            .AddMultiSig(fromKeys, m, pubKeys)
            .SignAsync()
            .ConfigureAwait(false);
    }
}
