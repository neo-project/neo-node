using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
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
        /// <param name="verificable">Transaction</param>
        /// <param name="contractParameters">Contract parameters</param>
        private VM.Types.StackItem OnInvokeWithResult(UInt160 scriptHash, string operation, IVerifiable verificable = null, JArray contractParameters = null)
        {
            List<ContractParameter> parameters = new List<ContractParameter>();

            if (contractParameters != null)
            {
                foreach (var contractParameter in contractParameters)
                {
                    parameters.Add(ContractParameter.FromJson(contractParameter));
                }
            }

            byte[] script;

            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(scriptHash, operation, parameters.ToArray());
                script = scriptBuilder.ToArray();
                Console.WriteLine($"Invoking script with: '{script.ToHexString()}'");
            }

            if (verificable is Transaction tx)
            {
                tx.Script = script;
            }

            using (ApplicationEngine engine = ApplicationEngine.Run(script, verificable, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Result Stack: {new JArray(engine.ResultStack.Select(p => p.ToJson()))}");
                Console.WriteLine();

                if (engine.State.HasFlag(VMState.FAULT) || !engine.ResultStack.TryPop(out VM.Types.StackItem ret))
                {
                    Console.WriteLine("Engine faulted.");
                    return null;
                }

                return ret;
            }
        }

        /// <summary>
        /// Process "invoke" command
        /// </summary>
        /// <param name="scriptHash">Script hash</param>
        /// <param name="operation">Operation</param>
        /// <param name="contractParameters">Contract parameters</param>
        /// <param name="witnessAddress">Witness address</param>
        [ConsoleCommand("invoke", Category = "Contract Commands")]
        private void OnInvokeCommand(UInt160 scriptHash, string operation, JArray contractParameters = null, UInt160[] witnessAddress = null)
        {
            List<Cosigner> signCollection = new List<Cosigner>();

            if (witnessAddress != null && !NoWallet())
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

            Transaction tx = new Transaction
            {
                Sender = UInt160.Zero,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
                Cosigners = signCollection.ToArray()
            };

            _ = OnInvokeWithResult(scriptHash, operation, tx, contractParameters);

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
