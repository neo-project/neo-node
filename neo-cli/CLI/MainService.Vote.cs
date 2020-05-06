using Neo.ConsoleService;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using Neo.Cryptography.ECC;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "register candidate" command
        /// </summary>
        /// <param name="senderAccount">Sender account</param>
        /// <param name="publicKey">Register publicKey</param>
        [ConsoleCommand("register candidate", Category = "Vote Commands")]
        private void OnRegisterCandidateCommand(UInt160 senderAccount, ECPoint publicKey)
        {
            if (NoWallet())
            {
                Console.WriteLine("Need open wallet!");
                return;
            }

            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "registerCandidate", publicKey);
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
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getCandidates");
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script);
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
        /// Process "get validators"
        /// </summary>
        [ConsoleCommand("get validators", Category = "Vote Commands")]
        private void OnGetValidatorsCommand()
        {
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getValidators");
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script);
        }

        /// <summary>
        /// Process "get committee"
        /// </summary>
        [ConsoleCommand("get committee", Category = "Vote Commands")]
        private void OnGetCommitteeCommand()
        {
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getCommittee");
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script);
        }

        /// <summary>
        /// Process "get next validators"
        /// </summary>
        [ConsoleCommand("get next validators", Category = "Vote Commands")]
        private void OnGetNextBlockValidatorsCommand()
        {
            byte[] script;
            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getNextBlockValidators");
                script = scriptBuilder.ToArray();
            }

            SendTransaction(script);
        }
    }
}
