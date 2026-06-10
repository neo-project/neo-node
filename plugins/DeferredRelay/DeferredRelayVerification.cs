// Copyright (C) 2015-2026 The Neo Project.
//
// DeferredRelayVerification.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using System.Diagnostics.CodeAnalysis;
using static Neo.SmartContract.Helper;

namespace Neo.Plugins.DeferredRelay;

/// <summary>
/// State-dependent verification for deferred relay, mirroring <see cref="Transaction.VerifyStateDependent"/>
/// but skipping the <see cref="VerifyResult.Expired"/> and <see cref="VerifyResult.NotYetValid"/> time gates.
/// </summary>
internal static class DeferredRelayVerification
{
    internal static bool IsDefinitiveRelayFailure(VerifyResult result) => result switch
    {
        VerifyResult.PolicyFail => true,
        VerifyResult.InvalidScript => true,
        VerifyResult.InvalidAttribute => true,
        VerifyResult.InvalidSignature => true,
        VerifyResult.Invalid => true,
        VerifyResult.OverSize => true,
        VerifyResult.InsufficientFunds => true,
        _ => false,
    };

    internal static VerifyResult VerifyForOffer(
        ProtocolSettings settings,
        DataCache snapshot,
        Transaction tx,
        TransactionVerificationContext context)
    {
        UInt160[] hashes = tx.GetScriptHashesForVerifying(snapshot);
        if (tx.Witnesses.Length != hashes.Length)
            return VerifyResult.InvalidSignature;
        foreach (UInt160 hash in hashes)
            if (NativeContract.Policy.IsBlocked(snapshot, hash))
                return VerifyResult.PolicyFail;

        if (!context.CheckTransaction(tx, [], snapshot))
            return VerifyResult.InsufficientFunds;

        long attributesFee = 0;
        uint height = NativeContract.Ledger.CurrentIndex(snapshot);
        foreach (TransactionAttribute attribute in tx.Attributes)
        {
            if (attribute.Type == TransactionAttributeType.NotaryAssisted && !settings.IsHardforkEnabled(Hardfork.HF_Echidna, height))
                return VerifyResult.InvalidAttribute;
            if (!attribute.Verify(snapshot, tx))
                return VerifyResult.InvalidAttribute;
            attributesFee += attribute.CalculateNetworkFee(snapshot, tx);
        }

        var netFeeDatoshi = tx.NetworkFee - (tx.Size * NativeContract.Policy.GetFeePerByte(snapshot)) - attributesFee;
        if (netFeeDatoshi < 0) return VerifyResult.InsufficientFunds;

        if (netFeeDatoshi > MaxVerificationGas) netFeeDatoshi = MaxVerificationGas;
        var execFeeFactor = NativeContract.Policy.GetExecFeeFactor(settings, snapshot, height);
        for (int i = 0; i < hashes.Length; i++)
        {
            if (IsSignatureContract(tx.Witnesses[i].VerificationScript.Span) &&
                IsSingleSignatureInvocationScript(tx.Witnesses[i].InvocationScript, out _))
            {
                netFeeDatoshi -= execFeeFactor * SignatureContractCost();
            }
            else if (IsMultiSigContract(tx.Witnesses[i].VerificationScript.Span, out int m, out int n) &&
                     IsMultiSignatureInvocationScript(m, tx.Witnesses[i].InvocationScript, out _))
            {
                netFeeDatoshi -= execFeeFactor * MultiSignatureContractCost(m, n);
            }
            else
            {
                if (!VerifyWitness(tx, settings, snapshot, hashes[i], tx.Witnesses[i], netFeeDatoshi, out long fee))
                    return VerifyResult.Invalid;
                netFeeDatoshi -= fee;
            }

            if (netFeeDatoshi < 0) return VerifyResult.InsufficientFunds;
        }

        return VerifyResult.Succeed;
    }

    private static bool IsMultiSignatureInvocationScript(int m, ReadOnlyMemory<byte> invocationScript,
        [NotNullWhen(true)] out ReadOnlyMemory<byte>[]? sigs)
    {
        sigs = null;
        ReadOnlySpan<byte> span = invocationScript.Span;
        int i = 0;
        var signatures = new List<ReadOnlyMemory<byte>>();
        while (i < invocationScript.Length)
        {
            if (span[i++] != (byte)OpCode.PUSHDATA1) return false;
            if (i + 65 > invocationScript.Length) return false;
            if (span[i++] != 64) return false;
            signatures.Add(invocationScript[i..(i + 64)]);
            i += 64;
        }

        if (signatures.Count != m) return false;
        sigs = signatures.ToArray();
        return true;
    }

    private static bool IsSingleSignatureInvocationScript(ReadOnlyMemory<byte> invocationScript,
        [NotNullWhen(true)] out ReadOnlyMemory<byte> sig)
    {
        sig = null;
        if (invocationScript.Length != 66) return false;
        ReadOnlySpan<byte> span = invocationScript.Span;
        if (span[0] != (byte)OpCode.PUSHDATA1 || span[1] != 64) return false;
        sig = invocationScript[2..66];
        return true;
    }

    // Mirrors Neo.SmartContract.Helper.VerifyWitness (internal) for deferred-queue verification.
    private static bool VerifyWitness(IVerifiable verifiable, ProtocolSettings settings, DataCache snapshot, UInt160 hash, Witness witness, long datoshi, out long fee)
    {
        fee = 0;
        Script invocationScript;
        try
        {
            invocationScript = new Script(witness.InvocationScript, true);
        }
        catch (BadScriptException)
        {
            return false;
        }

        using ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Verification, verifiable, snapshot.CloneCache(), null, settings, datoshi);
        if (witness.VerificationScript.Length == 0)
        {
            ContractState? cs = NativeContract.ContractManagement.GetContract(snapshot, hash);
            if (cs is null) return false;
            ContractMethodDescriptor? md = cs.Manifest.Abi.GetMethod(ContractBasicMethod.Verify, ContractBasicMethod.VerifyPCount);
            if (md?.ReturnType != ContractParameterType.Boolean) return false;
            engine.LoadContract(cs, md, CallFlags.ReadOnly);
        }
        else
        {
            if (NativeContract.IsNative(hash)) return false;
            if (hash != witness.ScriptHash) return false;
            Script verificationScript;
            try
            {
                verificationScript = new Script(witness.VerificationScript, true);
            }
            catch (BadScriptException)
            {
                return false;
            }

            engine.LoadScript(verificationScript, initialPosition: 0, configureState: p =>
            {
                p.CallFlags = CallFlags.ReadOnly;
                p.ScriptHash = hash;
            });
        }

        engine.LoadScript(invocationScript, configureState: p => p.CallFlags = CallFlags.None);

        if (engine.Execute() == VMState.FAULT) return false;
        if (engine.ResultStack.Count != 1) return false;
        try
        {
            if (!engine.ResultStack.Peek().GetBoolean()) return false;
        }
        catch
        {
            return false;
        }

        fee = engine.FeeConsumed;
        return true;
    }
}
