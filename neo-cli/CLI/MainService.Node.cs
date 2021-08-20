// Copyright (C) 2016-2021 The Neo Project.
// 
// The neo-cli is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "show pool" command
        /// </summary>
        [ConsoleCommand("show pool", Category = "Node Commands", Description = "Show the current state of the mempool")]
        private void OnShowPoolCommand(bool verbose = false)
        {
            int verifiedCount, unverifiedCount;
            if (verbose)
            {
                NeoSystem.MemPool.GetVerifiedAndUnverifiedTransactions(
                    out IEnumerable<Transaction> verifiedTransactions,
                    out IEnumerable<Transaction> unverifiedTransactions);
                Console.WriteLine("Verified Transactions:");
                foreach (Transaction tx in verifiedTransactions)
                    Console.WriteLine($" {tx.Hash} {tx.GetType().Name} {tx.NetworkFee} GAS_NetFee");
                Console.WriteLine("Unverified Transactions:");
                foreach (Transaction tx in unverifiedTransactions)
                    Console.WriteLine($" {tx.Hash} {tx.GetType().Name} {tx.NetworkFee} GAS_NetFee");

                verifiedCount = verifiedTransactions.Count();
                unverifiedCount = unverifiedTransactions.Count();
            }
            else
            {
                verifiedCount = NeoSystem.MemPool.VerifiedCount;
                unverifiedCount = NeoSystem.MemPool.UnVerifiedCount;
            }
            Console.WriteLine($"total: {NeoSystem.MemPool.Count}, verified: {verifiedCount}, unverified: {unverifiedCount}");
        }

        /// <summary>
        /// Process "show state" command
        /// </summary>
        [ConsoleCommand("show state", Category = "Node Commands", Description = "Show the current state of the node")]
        private void OnShowStateCommand()
        {
            var cancel = new CancellationTokenSource();

            Console.CursorVisible = false;
            Console.Clear();

            Task broadcast = Task.Run(async () =>
            {
                while (!cancel.Token.IsCancellationRequested)
                {
                    NeoSystem.LocalNode.Tell(Message.Create(MessageCommand.Ping, PingPayload.Create(NativeContract.Ledger.CurrentIndex(NeoSystem.StoreView))));
                    await Task.Delay(NeoSystem.Settings.TimePerBlock, cancel.Token);
                }
            });
            Task task = Task.Run(async () =>
            {
                int maxLines = 0;
                int loadingProgress = 0;
                uint previousHeight = 0;
                while (!cancel.Token.IsCancellationRequested)
                {
                    uint height = NativeContract.Ledger.CurrentIndex(NeoSystem.StoreView);
                    uint headerHeight = NeoSystem.HeaderCache.Last?.Index ?? height;
                    if (previousHeight == 0) previousHeight = headerHeight;

                    Console.SetCursorPosition(0, 0);
                    WriteLineWithoutFlicker($"block: {height}/{headerHeight}  connected: {LocalNode.ConnectedCount}  unconnected: {LocalNode.UnconnectedCount}", Console.WindowWidth - 1);

                    int linesWritten = 1;
                    string placeHolder = ". ";
                    if (headerHeight > previousHeight || loadingProgress > 30)
                    {
                        previousHeight = headerHeight;
                        loadingProgress = 0;
                        foreach (var value in Enumerable.Range(0, Console.WindowWidth))
                            placeHolder += " ";
                    }
                    else
                        foreach (var value in Enumerable.Range(0, loadingProgress))
                            placeHolder += ". ";

                    Console.WriteLine($"\r {placeHolder}");

                    loadingProgress++;
                    linesWritten++;

                    foreach (RemoteNode node in LocalNode.GetRemoteNodes().OrderByDescending(u => u.LastBlockIndex).Take(Console.WindowHeight - 2).ToArray())
                    {
                        ShowState($"\r  ip: ",
                            $"{ node.Remote.Address,-15}\t",
                            $"port: ",
                            $"{node.Remote.Port,-5}\t",
                            $"listen: ",
                            $"{node.ListenerTcpPort,-5}\t",
                            $"height: ",
                            $"{node.LastBlockIndex,-7}");

                        linesWritten++;
                    }

                    maxLines = Math.Max(maxLines, linesWritten);

                    while (linesWritten <= maxLines)
                    {
                        WriteLineWithoutFlicker("", Console.WindowWidth - 1);
                        maxLines--;
                    }

                    await Task.Delay(500, cancel.Token);
                }
            });
            ReadLine();
            cancel.Cancel();
            try { Task.WaitAll(task, broadcast); } catch { }
            Console.WriteLine();
            Console.CursorVisible = true;
        }

        private void ShowState(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                if (i % 2 == 1)
                    Console.ForegroundColor = ConsoleColor.White;
                Console.Write(values[i]);
            }

            Console.WriteLine();
        }
    }
}
