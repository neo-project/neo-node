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
using Neo.IO;
using Neo.Cryptography.ECC;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "register validator" command
        /// </summary>
        /// <param name="publicKey"></param>
        [ConsoleCommand("register candidate", Category = "Vote Commands")]
        private void OnRegisterCandidateCommand(UInt160 account, ECPoint[] publicKeys)
        {
            if (NoWallet()) return;

            List<Cosigner> signCollection = new List<Cosigner>();

            if (!NoWallet())
            {
                using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    UInt160[] accounts = CurrentWallet.GetAccounts().Where(p => !p.Lock && !p.WatchOnly).Select(p => p.ScriptHash).Where(p => NativeContract.GAS.BalanceOf(snapshot, p).Sign > 0).ToArray();
                    foreach (var signAccount in accounts)
                    {

                        if (account.Equals(signAccount))
                        {
                            signCollection.Add(new Cosigner() { Account = signAccount });
                            break;
                        }
                    }
                }
            }

            ScriptBuilder scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "registerCandidate", publicKeys[0]);

            Transaction tx = CurrentWallet.MakeTransaction(scriptBuilder.ToArray(), CurrentWallet.GetAccounts().FirstOrDefault().ScriptHash, null, signCollection.ToArray());
            Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }

            SignAndSendTx(tx);
            return;
        }

        /// <summary>
        /// Process "get validators"
        /// </summary>
        [ConsoleCommand("get candidates", Category = "Vote Commands")]
        private void OnGetCandidatesCommand()
        {
            ScriptBuilder scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getCandidates");      

            Transaction tx = CurrentWallet.MakeTransaction(scriptBuilder.ToArray(), CurrentWallet.GetAccounts().FirstOrDefault().ScriptHash);
            Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }

            SignAndSendTx(tx);
            return;
        }

        /// <summary>
        /// Process "unregister validator" command
        /// </summary>
        /// <param name="publicKey"></param>
        [ConsoleCommand("unregister candidate", Category = "Vote Commands")]
        private void OnUnregisterCandidateCommand(UInt160 account, ECPoint[] publicKeys)
        {
            if (NoWallet()) return;

            List<Cosigner> signCollection = new List<Cosigner>();

            if (!NoWallet())
            {
                using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    UInt160[] accounts = CurrentWallet.GetAccounts().Where(p => !p.Lock && !p.WatchOnly).Select(p => p.ScriptHash).Where(p => NativeContract.GAS.BalanceOf(snapshot, p).Sign > 0).ToArray();
                    foreach (var signAccount in accounts)
                    {

                        if (account.Equals(signAccount))
                        {
                            signCollection.Add(new Cosigner() { Account = signAccount });
                            break;
                        }
                    }
                }
            }

            ScriptBuilder scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "unregisterCandidate", publicKeys[0]);

            Transaction tx = CurrentWallet.MakeTransaction(scriptBuilder.ToArray(), CurrentWallet.GetAccounts().FirstOrDefault().ScriptHash, null, signCollection.ToArray());
            Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }

            SignAndSendTx(tx);
            return;
        }

        /// <summary>
        /// Process "vote" command
        /// </summary>  
        /// <param name="account"></param>
        /// <param name="publicKeys"></param>
        /// <param name="witnessAddress"></param>
        [ConsoleCommand("vote", Category = "Vote Commands")]
        private void OnVoteCommand(UInt160 account, ECPoint[] publicKeys)
        {
            if (NoWallet()) return;

            List<Cosigner> signCollection = new List<Cosigner>();

            if (!NoWallet())
            {
                using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    UInt160[] accounts = CurrentWallet.GetAccounts().Where(p => !p.Lock && !p.WatchOnly).Select(p => p.ScriptHash).Where(p => NativeContract.GAS.BalanceOf(snapshot, p).Sign > 0).ToArray();
                    foreach (var signAccount in accounts)
                    {

                        if (account.Equals(signAccount))
                        {
                            signCollection.Add(new Cosigner() { Account = signAccount });
                            break;
                        }
                    }
                }
            }

            ScriptBuilder scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "vote", account, publicKeys[0]);         

            Transaction tx = CurrentWallet.MakeTransaction(scriptBuilder.ToArray(), CurrentWallet.GetAccounts().FirstOrDefault().ScriptHash, null, signCollection.ToArray());
            Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }

            SignAndSendTx(tx);
            return;
        }


        /// <summary>
        /// Process "get validators"
        /// </summary>
        [ConsoleCommand("get validators", Category = "Vote Commands")]
        private void OnGetValidatorsCommand()
        {
            ScriptBuilder scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getValidators");    

            Transaction tx = CurrentWallet.MakeTransaction(scriptBuilder.ToArray(), CurrentWallet.GetAccounts().FirstOrDefault().ScriptHash);
            Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }

            SignAndSendTx(tx);
            return;
        }

        /// <summary>
        /// Process "get committee"
        /// </summary>
        [ConsoleCommand("get committee", Category = "Vote Commands")]
        private void OnGetCommitteeCommand()
        {
            ScriptBuilder scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getCommittee");             

            Transaction tx = CurrentWallet.MakeTransaction(scriptBuilder.ToArray(), CurrentWallet.GetAccounts().FirstOrDefault().ScriptHash);
            Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }

            SignAndSendTx(tx);
            return;
        }

        /// <summary>
        /// Process "get next validators"
        /// </summary>
        [ConsoleCommand("get next validators", Category = "Vote Commands")]
        private void OnGetNextBlockValidatorsCommand()
        {
            ScriptBuilder scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitAppCall(NativeContract.NEO.Hash, "getNextBlockValidators");            

            Transaction tx = CurrentWallet.MakeTransaction(scriptBuilder.ToArray(), CurrentWallet.GetAccounts().FirstOrDefault().ScriptHash);
            Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                Console.WriteLine();
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    Console.WriteLine("Engine faulted.");
                    return;
                }
            }

            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return;
            }

            SignAndSendTx(tx);
            return;
        }
    }
}
