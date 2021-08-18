// Copyright (C) 2016-2021 The Neo Project.
// 
// The neo-cli is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Linq;
using System.Numerics;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "deploy" command
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="manifestPath">Manifest path</param>
        [ConsoleCommand("deploy", Category = "Contract Commands")]
        private void OnDeployCommand(string filePath, string manifestPath = null)
        {
            if (NoWallet()) return;
            byte[] script = LoadDeploymentScript(filePath, manifestPath, out var nef, out var manifest);

            Transaction tx;
            try
            {
                tx = CurrentWallet.MakeTransaction(NeoSystem.StoreView, script);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
                return;
            }

            UInt160 hash = SmartContract.Helper.GetContractHash(tx.Sender, nef.CheckSum, manifest.Name);

            Console.WriteLine($"Contract hash: {hash}");
            Console.WriteLine($"Gas consumed: {new BigDecimal((BigInteger)tx.SystemFee, NativeContract.GAS.Decimals)}");
            Console.WriteLine($"Network fee: {new BigDecimal((BigInteger)tx.NetworkFee, NativeContract.GAS.Decimals)}");
            Console.WriteLine($"Total fee: {new BigDecimal((BigInteger)(tx.SystemFee + tx.NetworkFee), NativeContract.GAS.Decimals)} GAS");
            if (!ReadUserInput("Relay tx? (no|yes)").IsYes()) // Add this in case just want to get hash but not relay
            {
                return;
            }
            SignAndSendTx(NeoSystem.StoreView, tx);
        }

        /// <summary>
        /// Process "update" command
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="manifestPath">Manifest path</param>
        [ConsoleCommand("update", Category = "Contract Commands")]
        private void OnUpdateCommand(UInt160 scriptHash, string filePath, string manifestPath, UInt160 sender, UInt160[] signerAccounts = null)
        {
            Signer[] signers = Array.Empty<Signer>();

            if (NoWallet()) return;
            if (!NoWallet() && sender != null)
            {
                if (signerAccounts == null)
                    signerAccounts = new UInt160[1] { sender };
                else if (signerAccounts.Contains(sender) && signerAccounts[0] != sender)
                {
                    var signersList = signerAccounts.ToList();
                    signersList.Remove(sender);
                    signerAccounts = signersList.Prepend(sender).ToArray();
                }
                else if (!signerAccounts.Contains(sender))
                {
                    signerAccounts = signerAccounts.Prepend(sender).ToArray();
                }
                signers = signerAccounts.Select(p => new Signer() { Account = p, Scopes = WitnessScope.CalledByEntry }).ToArray();
            }

            Transaction tx = new Transaction
            {
                Signers = signers,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>()
            };

            try
            {
                byte[] script = LoadUpdateScript(scriptHash, filePath, manifestPath, out var nef, out var manifest);
                tx = CurrentWallet.MakeTransaction(NeoSystem.StoreView, script, sender, signers);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
                return;
            }

            ContractState contract = NativeContract.ContractManagement.GetContract(NeoSystem.StoreView, scriptHash);
            if (contract == null)
            {
                Console.WriteLine($"Can't upgrade, contract hash not exist: {scriptHash}");
            }
            else
            {
                Console.WriteLine($"Contract hash: {scriptHash}");
                Console.WriteLine($"Updated times: {contract.UpdateCounter}");
                Console.WriteLine($"Gas consumed: {new BigDecimal((BigInteger)tx.SystemFee, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Network fee: {new BigDecimal((BigInteger)tx.NetworkFee, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Total fee: {new BigDecimal((BigInteger)(tx.SystemFee + tx.NetworkFee), NativeContract.GAS.Decimals)} GAS");
                if (!ReadUserInput("Relay tx? (no|yes)").IsYes()) // Add this in case just want to get hash but not relay
                {
                    return;
                }
                SignAndSendTx(NeoSystem.StoreView, tx);
            }
        }

        /// <summary>
        /// Process "invoke" command
        /// </summary>
        /// <param name="scriptHash">Script hash</param>
        /// <param name="operation">Operation</param>
        /// <param name="contractParameters">Contract parameters</param>
        /// <param name="sender">Transaction's sender</param>
        /// <param name="signerAccounts">Signer's accounts</param>
        /// <param name="masGas">Max fee for running the script</param>
        [ConsoleCommand("invoke", Category = "Contract Commands")]
        private void OnInvokeCommand(UInt160 scriptHash, string operation, JArray contractParameters = null, UInt160 sender = null, UInt160[] signerAccounts = null, decimal maxGas = 20)
        {
            var gas = new BigDecimal(maxGas, NativeContract.GAS.Decimals);
            Signer[] signers = Array.Empty<Signer>();
            if (!NoWallet() && sender != null)
            {
                if (signerAccounts == null)
                    signerAccounts = new UInt160[1] { sender };
                else if (signerAccounts.Contains(sender) && signerAccounts[0] != sender)
                {
                    var signersList = signerAccounts.ToList();
                    signersList.Remove(sender);
                    signerAccounts = signersList.Prepend(sender).ToArray();
                }
                else if (!signerAccounts.Contains(sender))
                {
                    signerAccounts = signerAccounts.Prepend(sender).ToArray();
                }
                signers = signerAccounts.Select(p => new Signer() { Account = p, Scopes = WitnessScope.CalledByEntry }).ToArray();
            }

            Transaction tx = new Transaction
            {
                Signers = signers,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
            };

            if (!OnInvokeWithResult(scriptHash, operation, out _, tx, contractParameters, gas: (long)gas.Value)) return;

            if (NoWallet()) return;
            try
            {
                tx = CurrentWallet.MakeTransaction(NeoSystem.StoreView, tx.Script, sender, signers, maxGas: (long)gas.Value);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
                return;
            }
            Console.WriteLine($"Network fee: {new BigDecimal((BigInteger)tx.NetworkFee, NativeContract.GAS.Decimals)}");
            Console.WriteLine($"Total fee: {new BigDecimal((BigInteger)(tx.SystemFee + tx.NetworkFee), NativeContract.GAS.Decimals)} GAS");
            if (!ReadUserInput("Relay tx? (no|yes)").IsYes())
            {
                return;
            }
            SignAndSendTx(NeoSystem.StoreView, tx);
        }
    }
}
