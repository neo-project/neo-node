using Akka.Actor;
using Neo.CommandParser;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Capabilities;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.ComponentModel;
using System.Net;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "broadcast addr" command
        /// </summary>
        /// <param name="payload">Payload</param>
        /// <param name="port">Port</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "addr")]
        private void OnBroadcastAddressCommand(IPAddress payload, ushort port)
        {
            if (payload == null)
            {
                Console.WriteLine("You must input the payload to relay.");
                return;
            }

            OnBroadcastCommand(MessageCommand.Addr,
                AddrPayload.Create(
                    NetworkAddressWithTime.Create(
                        payload, DateTime.UtcNow.ToTimestamp(),
                        new FullNodeCapability(),
                        new ServerCapability(NodeCapabilityType.TcpServer, port))
                    ));
        }

        /// <summary>
        /// Process "broadcast block" command
        /// </summary>
        /// <param name="hash">Hash</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "block")]
        private void OnBroadcastGetBlocksByHashCommand(UInt256 hash)
        {
            OnBroadcastCommand(MessageCommand.Block, Blockchain.Singleton.GetBlock(hash));
        }

        /// <summary>
        /// Process "broadcast block" command
        /// </summary>
        /// <param name="height">Block index</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "block")]
        private void OnBroadcastGetBlocksByHeightCommand(uint height)
        {
            OnBroadcastCommand(MessageCommand.Block, Blockchain.Singleton.GetBlock(height));
        }

        /// <summary>
        /// Process "broadcast getblocks" command
        /// </summary>
        /// <param name="hash">Hash</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "getblocks")]
        private void OnBroadcastGetBlocksCommand(UInt256 hash)
        {
            OnBroadcastCommand(MessageCommand.GetBlocks, GetBlocksPayload.Create(hash));
        }

        /// <summary>
        /// Process "broadcast getheaders" command
        /// </summary>
        /// <param name="hash">Hash</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "getheaders")]
        private void OnBroadcastGetHeadersCommand(UInt256 hash)
        {
            OnBroadcastCommand(MessageCommand.GetHeaders, GetBlocksPayload.Create(hash));
        }

        /// <summary>
        /// Process "broadcast getdata" command
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="payload">Payload</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "getdata")]
        private void OnBroadcastGetDataCommand(InventoryType type, UInt256[] payload)
        {
            OnBroadcastCommand(MessageCommand.GetData, InvPayload.Create(type, payload));
        }

        /// <summary>
        /// Process "broadcast inv" command
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="payload">Payload</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "inv")]
        private void OnBroadcastInvCommand(InventoryType type, UInt256[] payload)
        {
            OnBroadcastCommand(MessageCommand.Inv, InvPayload.Create(type, payload));
        }

        /// <summary>
        /// Process "broadcast transaction" command
        /// </summary>
        /// <param name="hash">Hash</param>
        [Category("Network Commands")]
        [ConsoleCommand("broadcast", "transaction")]
        private void OnBroadcastTransactionCommand(UInt256 hash)
        {
            OnBroadcastCommand(MessageCommand.Transaction, Blockchain.Singleton.GetTransaction(hash));
        }

        private void OnBroadcastCommand(MessageCommand command, ISerializable ret)
        {
            NeoSystem.LocalNode.Tell(Message.Create(command, ret));
        }

        /// <summary>
        /// Process "relay" command
        /// </summary>
        /// <param name="jsonObjectToRelay">Json object</param>
        [Category("Network Commands")]
        [ConsoleCommand("relay")]
        private void OnRelayCommand([CaptureWholeArgument] JObject jsonObjectToRelay)
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
