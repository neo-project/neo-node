using System;
using Neo.ConsoleService;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native.Tokens;
using Neo.VM;
using Neo.Wallets;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Prints the last transactions in the blockchain
        /// </summary>
        /// <param name="desiredCount"></param>
        /// <returns></returns>
        [ConsoleCommand("show last-transactions", Category = "Show Commands")]
        private void OnShowLastTransactions(uint desiredCount)
        {
            if (desiredCount < 1)
            {
                Console.WriteLine("Minimum 1 transaction");
                return;
            }
            if (desiredCount > 100)
            {
                Console.WriteLine("Maximum 100 transactions");
                return;
            }

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var blockHash = snapshot.CurrentBlockHash;
                var countedTransactions = 0;
                var countedBlocks = 0;
                var maxBlocks = 10000;
                Block block = snapshot.GetBlock(blockHash);
                do
                {
                    foreach (var tx in block.Transactions)
                    {
                        Console.WriteLine(ToCLIString(tx, block.Timestamp));
                        countedTransactions++;
                        if (countedTransactions == desiredCount)
                            return;
                    }

                    block = snapshot.GetBlock(block.PrevHash);
                    countedBlocks++;
                    if (countedBlocks == maxBlocks)
                    {
                        var shouldContinue = ReadUserInput("Max searched blocks reached (10000), continue? ").IsYes();
                        if (shouldContinue)
                        {
                            countedBlocks = 0;
                        }
                        else
                        {
                            break;
                        }
                    }
                } while (block != null && desiredCount > countedTransactions);

            }
        }

        /// <summary>
        /// Prints the SmartContract ABI
        /// </summary>
        /// <param name="contractHash"></param>
        /// <returns></returns>
        [ConsoleCommand("show contract", Category = "Show Commands")]
        private void OnShowContract(string contractHash)
        {
            if (knownSmartContracts.ContainsKey(contractHash))
            {
                contractHash = knownSmartContracts[contractHash];
            }

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var contract160 = UInt160.Parse(contractHash);
                var smartContract = snapshot.Contracts.TryGet(contract160);
                if (smartContract != null)
                {
                    Console.WriteLine(ToCLIString(smartContract));
                }
                else
                {
                    Console.WriteLine("Contract not found.");
                }
            }
        }

        private string ToCLIString(ContractState c)
        {
            string output = "";
            output += $"Hash: {c.ScriptHash}\n";
            output += $"EntryPoint: \n";
            output += $"\tName: {c.Manifest.Abi.EntryPoint.Name}\n";
            output += $"\tParameters: \n";
            foreach (var parameter in c.Manifest.Abi.EntryPoint.Parameters)
            {
                output += $"\t\tName: {parameter.Name}\n";
                output += $"\t\tType: {parameter.Type}\n";
            }

            output += $"Methods: \n";
            foreach (var method in c.Manifest.Abi.Methods)
            {
                output += $"\tName: {method.Name}\n";
                output += $"\tReturn Type: {method.ReturnType}\n";
                if (method.Parameters.Length > 0)
                {
                    output += $"\tParameters: \n";
                    foreach (var parameter in method.Parameters)
                    {
                        output += $"\t\tName: {parameter.Name}\n";
                        output += $"\t\tType: {parameter.Type}\n";
                    }
                }

            }

            output += $"Events: \n";
            foreach (var abiEvent in c.Manifest.Abi.Events)
            {
                output += $"\tName: {abiEvent.Name}\n";
                if (abiEvent.Parameters.Length > 0)
                {
                    output += $"\tParameters: \n";
                    foreach (var parameter in abiEvent.Parameters)
                    {
                        output += $"\t\tName: {parameter.Name}\n";
                        output += $"\t\tType: {parameter.Type}\n";
                    }
                }
            }

            return output;
        }


        /// <summary>
        /// Find and print a transaction using its hash
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns></returns>
        [ConsoleCommand("show transaction", Category = "Show Commands")]
        private void OnShowTransaction(UInt256 transactionHash)
        {
            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var tx = snapshot.GetTransaction(transactionHash);
                if (tx != null)
                {
                    Console.WriteLine(ToCLIString(tx));
                }
            }
        }

        private string ToCLIString(Transaction t, ulong blockTimestamp = 0)
        {
            string output = "";
            output += $"Hash: {t.Hash}\n";
            if (blockTimestamp > 0)
            {
                var blockTime = UnixEpoch.AddMilliseconds(blockTimestamp);
                blockTime = TimeZoneInfo.ConvertTimeFromUtc(blockTime, TimeZoneInfo.Local);
                output += $"Timestamp: {blockTime.ToShortDateString()} {blockTime.ToLongTimeString()}\n";
            }
            output += $"NetFee: {new BigDecimal(t.NetworkFee, NeoToken.GAS.Decimals)}\n";
            output += $"SysFee: {new BigDecimal(t.SystemFee, NeoToken.GAS.Decimals)}\n";
            output += $"Sender: {t.Sender.ToAddress()}\n";
            if (t.Cosigners != null && t.Cosigners.Length > 0)
            {
                output += $"Cosigners:\n";
                foreach (var cosigner in t.Cosigners)
                {
                    output += ToCLIString(cosigner);
                }
            }

            output += $"Script:\n";
            output += DisassembleScript(t.Script);

            output += "Witnesses: \n";
            foreach (var witness in t.Witnesses)
            {
                output += $"{ToCLIString(witness)}";
            }

            return output;
        }

        /// <summary>
        /// Finds and print the block using it's scripthash or index
        /// </summary>
        /// <param name="blockHashOrId"></param>
        /// <returns></returns>
        [ConsoleCommand("show block", Category = "Show Commands")]
        private void OnShowBlock(string blockHashOrId)
        {
            if (UInt256.TryParse(blockHashOrId, out var blockHash))
            {
                if (Blockchain.Singleton.ContainsBlock(blockHash))
                {
                    var block = Blockchain.Singleton.GetBlock(blockHash);
                    Console.WriteLine($"Block:\n{ToCLIString(block)}");
                }
                else
                {
                    Console.WriteLine("Block not found");
                }
            }
            else
            {
                var blockIndex = uint.Parse(blockHashOrId);
                var header = Blockchain.Singleton.GetBlock(blockIndex);
                if (header != null)
                {
                    Console.WriteLine($"Block:\n{ToCLIString(header)}");
                }
            }
        }

        private string ToCLIString(Cosigner cosigner)
        {
            string output = $"\tAccount: {cosigner.Account}\n";

            output += "\tScope:\t";
            if (cosigner.Scopes == WitnessScope.Global)
            {
                output += $"Global\t";
            }
            if (cosigner.Scopes.HasFlag(WitnessScope.CalledByEntry))
            {
                output += $"CalledByEntry\t";
            }
            if (cosigner.Scopes.HasFlag(WitnessScope.CustomContracts))
            {
                output += $"CustomContract\t";
            }
            if (cosigner.Scopes.HasFlag(WitnessScope.CustomGroups))
            {
                output += $"CustomGroup\t";
            }

            output += "\n";

            if (cosigner.AllowedContracts != null && cosigner.AllowedContracts.Length > 0)
            {
                output += "Allowed contracts: \n";
                foreach (var allowedContract in cosigner.AllowedContracts)
                {
                    output += $"\t{allowedContract.ToString()}\n";
                }
            }

            if (cosigner.AllowedGroups != null && cosigner.AllowedGroups.Length > 0)
            {
                output += "Allowed groups: \n";
                foreach (var allowedGroup in cosigner.AllowedGroups)
                {
                    output += $"\t{allowedGroup.ToString()}\n";
                }
            }

            return output;
        }

        private string ToCLIString(Witness witness)
        {
            string output = "";
            output += $"Invocation: \n";
            output += DisassembleScript(witness.InvocationScript);
            output += $"Verification: \n";
            output += DisassembleScript(witness.VerificationScript);
            return output;
        }


        private string ToCLIString(NotifyEventArgs notification)
        {
            string output = "";
            var vmArray = (Neo.VM.Types.Array)notification.State;
            var notificationName = "";
            if (vmArray.Count > 0)
            {
                notificationName = vmArray[0].GetString();
            }

            var adapter = GetCliStringAdapter(notificationName);
            output += adapter(vmArray);

            return output;
        }

        private string ToCLIString(Block block)
        {
            var blockTime = UnixEpoch.AddMilliseconds(block.Timestamp);
            blockTime = TimeZoneInfo.ConvertTimeFromUtc(blockTime, TimeZoneInfo.Local);
            string output = "";
            output += $"Hash: {block.Hash}\n";
            output += $"Index: {block.Index}\n";
            output += $"Size: {block.Size}\n";
            output += $"PreviousBlockHash: {block.PrevHash}\n";
            output += $"MerkleRoot: {block.MerkleRoot}\n";
            output += $"Timestamp: {blockTime.ToShortDateString()} {blockTime.ToLongTimeString()}\n";
            output += $"NextConsensus: {block.NextConsensus}\n";
            output += $"Transactions:\n";
            foreach (Transaction t in block.Transactions)
            {
                output += $"{ToCLIString(t, block.Timestamp)}";
                output += $"\n";
            }
            output += $"Witnesses:\n";
            output += $"\tInvocation: {block.Witness.InvocationScript.ToHexString()}\n";
            output += $"\tVerification: {block.Witness.VerificationScript.ToHexString()}\n";
            return output;
        }
    }
}
