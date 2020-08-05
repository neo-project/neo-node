using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using System;
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
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
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
        private void OnInvokeCommand(UInt160 scriptHash, string operation, JArray contractParameters = null, UInt160[] signerAccounts = null, UInt160 sender = null)
        {
            Signer[] signers = Array.Empty<Signer>();
            if (signerAccounts != null && !NoWallet())
                signers = CurrentWallet.GetAccounts().Where(p => !p.Lock && signerAccounts.Contains(p.ScriptHash)).Select(p => new Signer() { Account = p.ScriptHash, Scopes = WitnessScope.CalledByEntry }).ToArray();
            if (sender != null)
                signers = signers.Append(new Signer() { Account = sender, Scopes = WitnessScope.CalledByEntry }).ToArray();

            Transaction tx = new Transaction
            {
                Signers = signers,
                Witnesses = Array.Empty<Witness>(),
            };

            _ = OnInvokeWithResult(scriptHash, operation, tx, contractParameters);

            if (NoWallet()) return;
            try
            {
                tx = CurrentWallet.MakeTransaction(tx.Script, sender, signers);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
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
