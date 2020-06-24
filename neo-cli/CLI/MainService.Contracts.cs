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
        /// <param name="contractParameters">Contract parameters</param>
        /// <param name="witnessAddress">Witness address</param>
        [ConsoleCommand("invoke", Category = "Contract Commands")]
        private void OnInvokeCommand(UInt160 scriptHash, string operation, JArray contractParameters = null, UInt160[] witnessAddress = null)
        {
            Cosigner[] cosigners = new Cosigner[0];
            if (witnessAddress != null && !NoWallet())
                cosigners = CurrentWallet.GetAccounts().Where(p => !p.Lock && !p.WatchOnly && witnessAddress.Contains(p.ScriptHash)).Select(p => new Cosigner() { Account = p.ScriptHash }).ToArray();

            Transaction tx = new Transaction
            {
                Sender = UInt160.Zero,
                Attributes = cosigners,
                Witnesses = Array.Empty<Witness>(),
            };

            _ = OnInvokeWithResult(scriptHash, operation, tx, contractParameters);

            if (NoWallet()) return;
            try
            {
                tx = CurrentWallet.MakeTransaction(tx.Script, null, tx.Attributes);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Error: insufficient balance.");
                return;
            }
            if (!ReadUserInput("Relay tx(no|yes)").IsYes())
            {
                return;
            }
            SignAndSendTx(tx);
        }
    }
}
