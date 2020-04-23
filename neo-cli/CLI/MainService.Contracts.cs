using Neo.ConsoleService;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Oracle;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <param name="parameters">Contract parameters</param>
        /// <param name="witnessAddress">Witness address</param>
        /// <param name="oracle">Oracle</param>
        [ConsoleCommand("invoke", Category = "Contract Commands")]
        private void OnInvokeCommand(UInt160 scriptHash, string operation, ContractParameter[] parameters = null, UInt160[] witnessAddress = null, OracleWalletBehaviour oracle = OracleWalletBehaviour.OracleWithAssert)
        {
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

            byte[] script;
            Transaction tx;

            using ScriptBuilder scriptBuilder = new ScriptBuilder();
            {
                scriptBuilder.EmitAppCall(scriptHash, operation, parameters ?? Array.Empty<ContractParameter>());
                script = scriptBuilder.ToArray();
                Console.WriteLine($"Invoking script with: '{script.ToHexString()}'");
            }

            try
            {
                if (NoWallet())
                {
                    TestScript(script, signCollection.ToArray(), oracle);
                    return;
                }

                tx = CurrentWallet.MakeTransaction(script, null, cosigners: signCollection.ToArray(), oracle: oracle);

                Console.WriteLine($"Tx Hash: {tx.Hash}");
                TestScript(tx.Script, signCollection.ToArray(), oracle);

                if (!ReadUserInput("relay tx(no|yes)").IsYes())
                {
                    return;
                }

                SignAndSendTx(tx);
            }
            catch (InvalidOperationException error)
            {
                TestScript(script, signCollection.ToArray(), oracle);
                Console.WriteLine("Error: " + error.Message);
            }
        }

        private void TestScript(byte[] script, Cosigner[] cosigners, OracleWalletBehaviour oracle)
        {
            var tx = new Transaction()
            {
                Script = script,
                Cosigners = cosigners,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
                Sender = cosigners.FirstOrDefault()?.Account ?? UInt160.Zero,
            };

            using ApplicationEngine engine = ApplicationEngine.Run(script, tx, testMode: true,
                oracle: oracle != OracleWalletBehaviour.WithoutOracle ? new OracleExecutionCache(OracleService.Process) : null);

            Console.WriteLine($"VM State: {engine.State}");
            Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
            Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToJson()))}");

            Console.WriteLine();
            if (engine.State.HasFlag(VMState.FAULT))
            {
                Console.WriteLine("Engine faulted.");
                return;
            }
        }
    }
}
