using Neo.CommandParser;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
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
        [ConsoleCommand("deploy", HelpCategory = "Contract Commands")]
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
            Console.WriteLine($"Gas: {tx.SystemFee}");
            Console.WriteLine();
            SignAndSendTx(tx);
        }

        /// <summary>
        /// Process "invoke" command
        /// </summary>
        /// <param name="scriptHash">Script hash</param>
        /// <param name="operation">Operation</param>
        /// <param name="contractParameters">Contract parameters</param>
        [ConsoleCommand("invoke", HelpCategory = "Contract Commands")]
        private void OnInvokeCommand(UInt160 scriptHash, string operation, JArray contractParameters)
        {
            List<ContractParameter> parameters = new List<ContractParameter>();
            foreach (var contractParameter in contractParameters)
            {
                parameters.Add(ContractParameter.FromJson(contractParameter));
            }

            Transaction tx = new Transaction
            {
                Sender = UInt160.Zero,
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
                Cosigners = Array.Empty<Cosigner>()
            };

            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(scriptHash, operation, parameters.ToArray());
                tx.Script = scriptBuilder.ToArray();
                Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");
            }

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {engine.GasConsumed}");
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
                tx = CurrentWallet.MakeTransaction(tx.Script);
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
