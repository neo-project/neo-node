// Copyright (C) 2015-2026 The Neo Project.
//
// MainService.Vote.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System.Numerics;
using Array = Neo.VM.Types.Array;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.CLI;

public static class VoteMethods
{
    public const string Register = "registerCandidate";
    public const string Unregister = "unregisterCandidate";
    public const string Vote = "vote";
    public const string GetAccountState = "getAccountState";
    public const string GetCandidates = "getCandidates";
    public const string GetCommittee = "getCommittee";
    public const string GetNextBlockValidators = "getNextBlockValidators";
}

partial class MainService
{
    /// <summary>
    /// Process "register candidate" command
    /// </summary>
    /// <param name="account">register account scriptHash</param>
    [ConsoleCommand("register candidate", Category = "Vote Commands")]
    private void OnRegisterCandidateCommand(UInt160 account)
    {
        if (NoWallet()) return;

        var currentAccount = GetValidAccountOrWarn(account);
        if (currentAccount == null) return;

        var publicKey = currentAccount.GetKey()?.PublicKey;
        if (publicKey is null)
        {
            ConsoleHelper.Error("Unable to get the public key of the account.");
            return;
        }

        var snapshot = NeoSystem.StoreView;
        var index = NativeContract.Ledger.CurrentIndex(snapshot);
        if (NeoSystem.Settings.IsHardforkEnabled(Hardfork.HF_Echidna, index))
        {
            RegisterCandidateViaNep17Payment(account, publicKey, snapshot);
            return;
        }

        var testGas = NativeContract.NEO.GetRegisterPrice(snapshot) + BigInteger.Pow(10, NativeContract.GAS.Decimals) * 10;
        var script = BuildNeoScript(VoteMethods.Register, publicKey);
        RegisterCandidateViaInvoke(script, account, snapshot, (long)testGas);
    }

    private void RegisterCandidateViaInvoke(byte[] script, UInt160 account, DataCache snapshot, long testGas)
    {
        var signers = CurrentWallet!.GetAccounts()
            .Where(p => !p.Lock && !p.WatchOnly && p.ScriptHash == account && NativeContract.GAS.BalanceOf(snapshot, p.ScriptHash).Sign > 0)
            .Select(p => new Signer { Account = p.ScriptHash, Scopes = WitnessScope.CalledByEntry })
            .ToArray();

        try
        {
            var tx = CurrentWallet!.MakeTransaction(snapshot, script, account, signers, maxGas: testGas);
            ConsoleHelper.Info("Invoking script with: ", $"'{Convert.ToBase64String(tx.Script.Span)}'");
            using (var engine = ApplicationEngine.Run(tx.Script, snapshot, container: tx, settings: NeoSystem.Settings, gas: testGas))
            {
                PrintExecutionOutput(engine, true);
                if (engine.State == VMState.FAULT) return;
            }

            ConsoleHelper.Info(
                "Network fee: ", $"{new BigDecimal((BigInteger)tx.NetworkFee, NativeContract.GAS.Decimals)}\t",
                "Total fee: ", $"{new BigDecimal((BigInteger)(tx.SystemFee + tx.NetworkFee), NativeContract.GAS.Decimals)} GAS");

            if (!ConsoleHelper.ReadUserInput("Relay tx(no|yes)").IsYes())
                return;

            SignAndSendTx(snapshot, tx);
        }
        catch (InvalidOperationException e)
        {
            ConsoleHelper.Error(GetExceptionMessage(e));
        }
    }

    /// <summary>
    /// Registers a candidate by transferring the register price in GAS to the NEO native contract.
    /// This triggers <c>OnNEP17Payment</c> and avoids charging the register price as transaction system fee,
    /// which is required when <c>MaxBlockSystemFee</c> is lower than the register price.
    /// </summary>
    private void RegisterCandidateViaNep17Payment(UInt160 account, ECPoint publicKey, DataCache snapshot)
    {
        var registerPrice = NativeContract.NEO.GetRegisterPrice(snapshot);
        var amount = new BigDecimal((BigInteger)registerPrice, NativeContract.GAS.Decimals);
        var gasBalance = NativeContract.GAS.BalanceOf(snapshot, account);
        if (gasBalance < registerPrice)
        {
            ConsoleHelper.Error($"Insufficient GAS. Required: {amount}, available: {new BigDecimal(gasBalance, NativeContract.GAS.Decimals)}.");
            return;
        }

        Transaction tx;
        try
        {
            // OnNEP17Payment runs inside GAS.transfer callback (depth > 1), so CalledByEntry
            // witness scope is not sufficient for RegisterInternal's CheckWitness.
            var signers = new[]
            {
                new Signer { Account = account, Scopes = WitnessScope.Global }
            };
            tx = CurrentWallet!.MakeTransaction(snapshot, new[]
            {
                new TransferOutput
                {
                    AssetId = NativeContract.GAS.Hash,
                    Value = amount,
                    ScriptHash = NativeContract.NEO.Hash,
                    Data = publicKey.EncodePoint(true)
                }
            }, from: account, cosigners: signers);
        }
        catch (InvalidOperationException e)
        {
            ConsoleHelper.Error(GetExceptionMessage(e));
            return;
        }

        using (var engine = ApplicationEngine.Run(tx.Script, snapshot, container: tx, settings: NeoSystem.Settings, gas: TestModeGas))
        {
            PrintExecutionOutput(engine, true);
            if (engine.State == VMState.FAULT) return;
        }

        ConsoleHelper.Info("Registering candidate via GAS transfer to NEO contract, amount: ", amount.ToString());
        ConsoleHelper.Info(
            "Network fee: ", $"{new BigDecimal((BigInteger)tx.NetworkFee, NativeContract.GAS.Decimals)}\t",
            "System fee: ", $"{new BigDecimal((BigInteger)tx.SystemFee, NativeContract.GAS.Decimals)}\t",
            "Total fee: ", $"{new BigDecimal((BigInteger)(tx.SystemFee + tx.NetworkFee), NativeContract.GAS.Decimals)} GAS");

        if (!ConsoleHelper.ReadUserInput("Relay tx(no|yes)").IsYes())
            return;

        SignAndSendTx(snapshot, tx);
    }

    /// <summary>
    /// Process "unregister candidate" command
    /// </summary>
    /// <param name="account">unregister account scriptHash</param>
    [ConsoleCommand("unregister candidate", Category = "Vote Commands")]
    private void OnUnregisterCandidateCommand(UInt160 account)
    {
        if (NoWallet()) return;

        var currentAccount = GetValidAccountOrWarn(account);
        if (currentAccount == null) return;

        var publicKey = currentAccount?.GetKey()?.PublicKey;
        var script = BuildNeoScript(VoteMethods.Unregister, publicKey);
        SendTransaction(script, account);
    }

    /// <summary>
    /// Process "vote" command
    /// </summary>  
    /// <param name="senderAccount">Sender account</param>
    /// <param name="publicKey">Voting publicKey</param>
    [ConsoleCommand("vote", Category = "Vote Commands")]
    private void OnVoteCommand(UInt160 senderAccount, ECPoint publicKey)
    {
        if (NoWallet()) return;

        var script = BuildNeoScript(VoteMethods.Vote, senderAccount, publicKey);
        SendTransaction(script, senderAccount);
    }

    /// <summary>
    /// Process "unvote" command
    /// </summary>  
    /// <param name="senderAccount">Sender account</param>
    [ConsoleCommand("unvote", Category = "Vote Commands")]
    private void OnUnvoteCommand(UInt160 senderAccount)
    {
        if (NoWallet()) return;

        var script = BuildNeoScript(VoteMethods.Vote, senderAccount, null);
        SendTransaction(script, senderAccount);
    }

    /// <summary>
    /// Process "get candidates"
    /// </summary>
    [ConsoleCommand("get candidates", Category = "Vote Commands")]
    private void OnGetCandidatesCommand()
    {
        if (!OnInvokeWithResult(NativeContract.NEO.Hash, VoteMethods.GetCandidates, out var result, null, null, false)) return;

        var resJArray = (Array)result;

        if (resJArray.Count > 0)
        {
            Console.WriteLine();
            ConsoleHelper.Info("Candidates:");

            foreach (var item in resJArray)
            {
                var value = (Array)item;
                if (value is null) continue;

                Console.Write(((ByteString)value[0])?.GetSpan().ToHexString() + "\t");
                Console.WriteLine(((Integer)value[1]).GetInteger());
            }
        }
    }

    /// <summary>
    /// Process "get committee"
    /// </summary>
    [ConsoleCommand("get committee", Category = "Vote Commands")]
    private void OnGetCommitteeCommand()
    {
        if (!OnInvokeWithResult(NativeContract.NEO.Hash, VoteMethods.GetCommittee, out StackItem result, null, null, false)) return;

        var resJArray = (Array)result;

        if (resJArray.Count > 0)
        {
            Console.WriteLine();
            ConsoleHelper.Info("Committee:");

            foreach (var item in resJArray)
            {
                Console.WriteLine(((ByteString)item)?.GetSpan().ToHexString());
            }
        }
    }

    /// <summary>
    /// Process "get next validators"
    /// </summary>
    [ConsoleCommand("get next validators", Category = "Vote Commands")]
    private void OnGetNextBlockValidatorsCommand()
    {
        if (!OnInvokeWithResult(NativeContract.NEO.Hash, VoteMethods.GetNextBlockValidators, out var result, null, null, false)) return;

        var resJArray = (Array)result;

        if (resJArray.Count > 0)
        {
            Console.WriteLine();
            ConsoleHelper.Info("Next validators:");

            foreach (var item in resJArray)
            {
                Console.WriteLine(((ByteString)item)?.GetSpan().ToHexString());
            }
        }
    }

    /// <summary>
    /// Process "get accountstate"
    /// </summary>
    [ConsoleCommand("get accountstate", Category = "Vote Commands")]
    private void OnGetAccountState(UInt160 address)
    {
        const string Notice = "No vote record!";
        var arg = new JObject
        {
            ["type"] = "Hash160",
            ["value"] = address.ToString()
        };

        if (!OnInvokeWithResult(NativeContract.NEO.Hash, VoteMethods.GetAccountState, out var result, null, new JArray(arg))) return;
        Console.WriteLine();
        if (result.IsNull)
        {
            ConsoleHelper.Warning(Notice);
            return;
        }
        var resJArray = (Array)result;
        if (resJArray is null)
        {
            ConsoleHelper.Warning(Notice);
            return;
        }

        foreach (var value in resJArray)
        {
            if (value.IsNull)
            {
                ConsoleHelper.Warning(Notice);
                return;
            }
        }

        var hexPubKey = ((ByteString)resJArray[2])?.GetSpan().ToHexString();
        if (string.IsNullOrEmpty(hexPubKey))
        {
            ConsoleHelper.Error("Error parsing the result");
            return;
        }

        if (ECPoint.TryParse(hexPubKey, ECCurve.Secp256r1, out var publickey))
        {
            ConsoleHelper.Info("Voted: ", Contract.CreateSignatureRedeemScript(publickey).ToScriptHash().ToAddress(NeoSystem.Settings.AddressVersion));
            ConsoleHelper.Info("Amount: ", new BigDecimal(((Integer)resJArray[0]).GetInteger(), NativeContract.NEO.Decimals).ToString());
            ConsoleHelper.Info("Block: ", ((Integer)resJArray[1]).GetInteger().ToString());
        }
        else
        {
            ConsoleHelper.Error("Error parsing the result");
        }
    }
    /// <summary>
    /// Get account or log a warm
    /// </summary>
    /// <param name="account"></param>
    /// <returns>account or null</returns>
    private WalletAccount? GetValidAccountOrWarn(UInt160 account)
    {
        var acct = CurrentWallet?.GetAccount(account);
        if (acct == null)
        {
            ConsoleHelper.Warning("This address isn't in your wallet!");
            return null;
        }
        if (acct.Lock || acct.WatchOnly)
        {
            ConsoleHelper.Warning("Locked or WatchOnly address.");
            return null;
        }
        return acct;
    }

    private byte[] BuildNeoScript(string method, params object?[] args)
        => NativeContract.NEO.Hash.MakeScript(method, args);
}
