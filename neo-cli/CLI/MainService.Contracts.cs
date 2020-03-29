using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Neo.CLI
{
    partial class MainService
    {
        private class AttachAsset
        {
            public AssetDescriptor Asset;
            public BigDecimal Amount;

            /// <summary>
            /// Parse from string
            ///     Format: neo=1   gas=2.456   0x0..=123.5
            /// </summary>
            /// <param name="input">Input</param>
            /// <returns></returns>
            internal static AttachAsset Parse(string input)
            {
                var split = input.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (split.Length != 2) throw new FormatException("Format expected: neo=1");

                var ret = new AttachAsset
                {
                    Asset = (split[0].ToLowerInvariant()) switch
                    {
                        "neo" => new AssetDescriptor(NativeContract.NEO.Hash),
                        "gas" => new AssetDescriptor(NativeContract.GAS.Hash),
                        _ => new AssetDescriptor(UInt160.Parse(split[0])),
                    }
                };

                ret.Amount = BigDecimal.Parse(split[1], ret.Asset.Decimals);
                return ret;
            }
        }

        /// <summary>
        /// Process "deploy" command
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="manifestPath">Manifest path</param>
        [ConsoleCommand("deploy", Category = "Contract Commands")]
        private void OnDeployCommand(string filePath, string manifestPath = null)
        {
            if (NoWallet()) return;
            byte[] script = LoadDeploymentScript(filePath, manifestPath, out var scriptHash);

            Transaction tx;
            try
            {
                tx = CurrentWallet.MakeTransaction(script);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Engine faulted.");
                return;
            }
            Console.WriteLine($"Script hash: {scriptHash.ToString()}");
            Console.WriteLine($"Gas: {new BigDecimal(tx.SystemFee, NativeContract.GAS.Decimals)}");
            Console.WriteLine();
            SignAndSendTx(tx);
        }

        /// <summary>
        /// Process "invoke" command
        /// </summary>
        /// <param name="scriptHash">Script hash</param>
        /// <param name="operation">Operation</param>
        /// <param name="contractParameters">Contract parameters</param>
        /// <param name="witnessAddress">Witness address</param>
        /// <param name="attach">Attach</param>
        [ConsoleCommand("invoke", Category = "Contract Commands")]
        private void OnInvokeCommand(UInt160 scriptHash, string operation, JArray contractParameters = null, UInt160[] witnessAddress = null, AttachAsset[] attach = null)
        {
            List<ContractParameter> parameters = new List<ContractParameter>();
            List<Cosigner> signCollection = new List<Cosigner>();

            if (!NoWallet() && witnessAddress != null)
            {
                using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    UInt160[] accounts = CurrentWallet.GetAccounts().Where(p => !p.Lock && !p.WatchOnly).Select(p => p.ScriptHash).Where(p => NativeContract.GAS.BalanceOf(snapshot, p).Sign > 0).ToArray();
                    foreach (var signAccount in accounts)
                    {
                        if (witnessAddress is null)
                        {
                            break;
                        }
                        foreach (var witness in witnessAddress)
                        {
                            if (witness.Equals(signAccount))
                            {
                                signCollection.Add(new Cosigner() { Account = signAccount });
                                break;
                            }
                        }
                    }
                }
            }

            if (contractParameters != null)
            {
                foreach (var contractParameter in contractParameters)
                {
                    parameters.Add(ContractParameter.FromJson(contractParameter));
                }
            }

            Transaction tx = new Transaction
            {
                Sender = UInt160.Zero,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
                Cosigners = signCollection.ToArray()
            };

            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                if (attach != null)
                {
                    if (NoWallet()) return;

                    foreach (var entry in attach.GroupBy(u => u.Asset.AssetId))
                    {
                        // Get amount

                        var amount = BigInteger.Zero;
                        foreach (var am in entry) amount += am.Amount.Value;
                        
                        // Find a valid sender

                        var tempTx = CurrentWallet.MakeTransaction(new[]
                        {
                            new TransferOutput
                            {
                                AssetId = entry.Key,
                                Value = new BigDecimal(amount, entry.First().Asset.Decimals),
                                ScriptHash = scriptHash
                            }
                        });

                        if (tempTx == null)
                        {
                            Console.WriteLine("Insufficient funds of: " + entry.First().Asset.AssetName);
                            return;
                        }

                        // Append at the begining the right script

                        scriptBuilder.EmitAppCall(entry.Key, "transfer", tempTx.Sender, scriptHash, amount);

                        // Compute new cosigners

                        signCollection.Add(new Cosigner()
                        {
                            Account = tempTx.Sender,
                            AllowedContracts = new UInt160[] { entry.Key },
                            Scopes = WitnessScope.CustomContracts
                        });
                    }

                    // Add new cosigners

                    tx.Cosigners = signCollection.ToArray();
                }

                scriptBuilder.EmitAppCall(scriptHash, operation, parameters.ToArray());
                tx.Script = scriptBuilder.ToArray();
                Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");
            }

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (NoWallet()) return;
            try
            {
                tx = CurrentWallet.MakeTransaction(tx.Script, null, tx.Attributes, tx.Cosigners);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Error: insufficient balance.");
                return;
            }
            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }
            SignAndSendTx(tx);
        }
    }
}
