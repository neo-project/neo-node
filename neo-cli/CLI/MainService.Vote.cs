using Neo.ConsoleService;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using Neo.Cryptography.ECC;
using Neo.VM.Types;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "register candidate" command
        /// </summary>
        /// <param name="senderAccount">Sender account</param>
        /// <param name="senderPublicKey">Register publicKey</param>
        [ConsoleCommand("register candidate", Category = "Vote Commands")]
        private void OnRegisterCandidateCommand(UInt160 senderAccount, ECPoint senderPublicKey)
        {
            if (NoWallet())
            {
                Console.WriteLine("Need open wallet!");
                return;
            }

            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "registerCandidate", senderPublicKey);
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script, senderAccount);
        }

        /// <summary>
        /// Process "unregister candidate" command
        /// </summary>
        /// <param name="senderAccount">Sender account</param>
        /// <param name="publicKey">Unregister publicKey</param>
        [ConsoleCommand("unregister candidate", Category = "Vote Commands")]
        private void OnUnregisterCandidateCommand(UInt160 senderAccount, ECPoint publicKey)
        {
            if (NoWallet())
            {
                Console.WriteLine("Need open wallet!");
                return;
            }

            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "unregisterCandidate", publicKey);
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script, senderAccount);
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
                    Console.WriteLine(((Integer)value?[1]).ToBigInteger());
                }
            }
        }

        /// <summary>
        /// Process "get validators"
        /// </summary>
        [ConsoleCommand("get validators", Category = "Vote Commands")]
        private void OnGetValidatorsCommand()
        {
            var result = OnInvokeWithResult(NativeContract.NEO.Hash, "getValidators", null, null, false);

            var resJArray = (VM.Types.Array)result;

            if (resJArray.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Validators:");

                foreach (var item in resJArray)
                {
                    Console.WriteLine(((ByteString)item)?.GetSpan().ToHexString());
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
