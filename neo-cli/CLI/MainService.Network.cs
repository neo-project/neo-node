using Akka.Actor;
using Neo.CLI.CommandParser;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Capabilities;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Net;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "broadcast" command
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="payload">Payload</param>
        [ConsoleCommand("broadcast", HelpCategory = "Network Commands", ExcludeIfAmbiguous = true)]
        private void OnBroadcastCommand(MessageCommand command, string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                Console.WriteLine("You must input the payload to relay.");
                return;
            }

            OnBroadcastCommandInternal(command, payload, null);
        }

        /// <summary>
        /// Process "broadcast" command (GetData/Inv)
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="payload">Payload</param>
        /// <param name="hashes">Hashes</param>
        [ConsoleCommand("broadcast", HelpCategory = "Network Commands")]
        private void OnBroadcastCommand(MessageCommand command, string payload, params UInt256[] hashes)
        {
            if (string.IsNullOrEmpty(payload))
            {
                Console.WriteLine("You must input the payload to relay.");
                return;
            }

            OnBroadcastCommandInternal(command, payload, hashes);
        }

        /// <summary>
        /// Process "broadcast" command (Addr)
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="payload">Payload</param>
        /// <param name="port">Port</param>
        [ConsoleCommand("broadcast", HelpCategory = "Network Commands")]
        private void OnBroadcastCommand(MessageCommand command, string payload, ushort port)
        {
            if (string.IsNullOrEmpty(payload))
            {
                Console.WriteLine("You must input the payload to relay.");
                return;
            }

            OnBroadcastCommandInternal(command, payload, port);
        }

        private void OnBroadcastCommandInternal(MessageCommand command, string payload, object extraPayload)
        {
            ISerializable ret = null;
            switch (command)
            {
                case MessageCommand.Addr:
                    ret = AddrPayload.Create(NetworkAddressWithTime.Create(IPAddress.Parse(payload), DateTime.UtcNow.ToTimestamp(), new FullNodeCapability(), new ServerCapability(NodeCapabilityType.TcpServer, (ushort)extraPayload)));
                    break;
                case MessageCommand.Block:
                    if (payload.Length == 64 || payload.Length == 66)
                        ret = Blockchain.Singleton.GetBlock(UInt256.Parse(payload));
                    else
                        ret = Blockchain.Singleton.GetBlock(uint.Parse(payload));
                    break;
                case MessageCommand.GetBlocks:
                case MessageCommand.GetHeaders:
                    ret = GetBlocksPayload.Create(UInt256.Parse(payload));
                    break;
                case MessageCommand.GetData:
                case MessageCommand.Inv:
                    ret = InvPayload.Create(Enum.Parse<InventoryType>(payload, true), (UInt256[])extraPayload);
                    break;
                case MessageCommand.Transaction:
                    ret = Blockchain.Singleton.GetTransaction(UInt256.Parse(payload));
                    break;
                default:
                    Console.WriteLine($"Command \"{command}\" is not supported.");
                    return;
            }
            NeoSystem.LocalNode.Tell(Message.Create(command, ret));
        }

        /// <summary>
        /// Process "relay" command
        /// </summary>
        /// <param name="jsonObjectToRelay">Json object</param>
        [ConsoleCommand("relay", HelpCategory = "Network Commands")]
        private void OnRelayCommand(JObject jsonObjectToRelay)
        {
            if (jsonObjectToRelay == null)
            {
                Console.WriteLine("You must input JSON object to relay.");
                return;
            }

            try
            {
                ContractParametersContext context = ContractParametersContext.Parse(jsonObjectToRelay.ToString());
                if (!context.Completed)
                {
                    Console.WriteLine("The signature is incomplete.");
                    return;
                }
                if (!(context.Verifiable is Transaction tx))
                {
                    Console.WriteLine($"Only support to relay transaction.");
                    return;
                }
                tx.Witnesses = context.GetWitnesses();
                NeoSystem.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                Console.WriteLine($"Data relay success, the hash is shown as follows:{Environment.NewLine}{tx.Hash}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"One or more errors occurred:{Environment.NewLine}{e.Message}");
            }
        }
    }
}
