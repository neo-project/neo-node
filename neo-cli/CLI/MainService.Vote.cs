using Neo.ConsoleService;
using Neo.Cryptography.ECC;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using System;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "register candidate" command
        /// </summary>
        /// <param name="account">register account scriptHash</param>        
        [ConsoleCommand("register candidate", Category = "Vote Commands")]
        private void OnRegisterCandidateCommand(UInt160 account)
        {
            if (NoWallet())
            {
                Console.WriteLine("Need open wallet!");
                return;
            }

            ECPoint publicKey = CurrentWallet.GetAccount(account)?.GetKey()?.PublicKey;
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "registerCandidate", publicKey);
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script, account);
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

            ECPoint publicKey = CurrentWallet.GetAccount(account)?.GetKey()?.PublicKey;
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "unregisterCandidate", publicKey);
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
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "vote", senderAccount, publicKey);
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
            var result = OnInvokeWithResult(NativeContract.NEO.Hash, "getCandidates", null, null, false);

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
            var result = OnInvokeWithResult(NativeContract.NEO.Hash, "getCommittee", null, null, false);

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
            var result = OnInvokeWithResult(NativeContract.NEO.Hash, "getNextBlockValidators", null, null, false);

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
