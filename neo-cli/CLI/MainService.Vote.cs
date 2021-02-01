using Neo.ConsoleService;
using Neo.Cryptography.ECC;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;

namespace Neo.CLI
{
    partial class MainService
    {
        public const uint RegisterGas = 1010;

        /// <summary>
        /// Process "register candidate" command
        /// </summary>
        /// <param name="account">register account scriptHash</param>
        /// <param name="maxGas">Max fee for running the script</param>
        [ConsoleCommand("register candidate", Category = "Vote Commands")]
        private void OnRegisterCandidateCommand(UInt160 account, decimal maxGas = RegisterGas)
        {
            var gas = new BigDecimal(maxGas, NativeContract.GAS.Decimals);

            if (NoWallet())
            {
                Console.WriteLine("Need open wallet!");
                return;
            }

            WalletAccount currentAccount = CurrentWallet.GetAccount(account);

            if (currentAccount == null)
            {
                Console.WriteLine("This address isn't in your wallet!");
                return;
            }
            else
            {
                if (currentAccount.Lock || currentAccount.WatchOnly)
                {
                    Console.WriteLine("Locked or WatchOnly address.");
                    return;
                }
            }

            ECPoint publicKey = currentAccount?.GetKey()?.PublicKey;
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitDynamicCall(NativeContract.NEO.Hash, "registerCandidate", publicKey);
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script, account, (long)gas.Value);
        }

        /// <summary>
        /// Process "unregister candidate" command
        /// </summary>
        /// <param name="account">unregister account scriptHash</param>        
        [ConsoleCommand("unregister candidate", Category = "Vote Commands")]
        private void OnUnregisterCandidateCommand(UInt160 account)
        {
            if (NoWallet())
            {
                Console.WriteLine("Need open wallet!");
                return;
            }

            WalletAccount currentAccount = CurrentWallet.GetAccount(account);

            if (currentAccount == null)
            {
                Console.WriteLine("This address isn't in your wallet!");
                return;
            }
            else
            {
                if (currentAccount.Lock || currentAccount.WatchOnly)
                {
                    Console.WriteLine("Locked or WatchOnly address.");
                    return;
                }
            }

            ECPoint publicKey = currentAccount?.GetKey()?.PublicKey;
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitDynamicCall(NativeContract.NEO.Hash, "unregisterCandidate", publicKey);
                script = scriptBuilder.ToArray();
            }

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
            if (NoWallet())
            {
                Console.WriteLine("Need open wallet!");
                return;
            }

            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitDynamicCall(NativeContract.NEO.Hash, "vote", senderAccount, publicKey);
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script, senderAccount);
        }

        /// <summary>
        /// Process "get candidates"
        /// </summary>
        [ConsoleCommand("get candidates", Category = "Vote Commands")]
        private void OnGetCandidatesCommand()
        {
            if (!OnInvokeWithResult(NativeContract.NEO.Hash, "getCandidates", out StackItem result, null, null, false)) return;

            var resJArray = (VM.Types.Array)result;

            if (resJArray.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Candidates:");

                foreach (var item in resJArray)
                {
                    var value = (VM.Types.Array)item;

                    Console.Write(((ByteString)value?[0])?.GetSpan().ToHexString() + "\t");
                    Console.WriteLine(((Integer)value?[1]).GetInteger());
                }
            }
        }

        /// <summary>
        /// Process "get committee"
        /// </summary>
        [ConsoleCommand("get committee", Category = "Vote Commands")]
        private void OnGetCommitteeCommand()
        {
            if (!OnInvokeWithResult(NativeContract.NEO.Hash, "getCommittee", out StackItem result, null, null, false)) return;

            var resJArray = (VM.Types.Array)result;

            if (resJArray.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Committee:");

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
            if (!OnInvokeWithResult(NativeContract.NEO.Hash, "getNextBlockValidators", out StackItem result, null, null, false)) return;

            var resJArray = (VM.Types.Array)result;

            if (resJArray.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Next validators:");

                foreach (var item in resJArray)
                {
                    Console.WriteLine(((ByteString)item)?.GetSpan().ToHexString());
                }
            }
        }
    }
}
