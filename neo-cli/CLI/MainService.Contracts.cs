using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
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
                tx = CurrentWallet.MakeTransaction(script);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
                return;
            }

            UInt160 hash = SmartContract.Helper.GetContractHash(tx.Sender, nef.CheckSum, manifest.Name);

            Console.WriteLine($"Contract hash: {hash}");
            Console.WriteLine($"Gas: {new BigDecimal((BigInteger)tx.SystemFee, NativeContract.GAS.Decimals)}");
            Console.WriteLine();
            SignAndSendTx(tx);
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
            if (signerAccounts != null && !NoWallet())
            {
                if (sender != null)
                {
                    if (signerAccounts.Contains(sender) && signerAccounts[0] != sender)
                    {
                        var signersList = signerAccounts.ToList();
                        signersList.Remove(sender);
                        signerAccounts = signersList.Prepend(sender).ToArray();
                    }
                    else if (!signerAccounts.Contains(sender))
                    {
                        signerAccounts = signerAccounts.Prepend(sender).ToArray();
                    }
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
                tx = CurrentWallet.MakeTransaction(tx.Script, sender, signers, maxGas: (long)gas.Value);
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
