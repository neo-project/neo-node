using Neo.CommandParser;
using Neo.Ledger;
using System;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "export blocks" command
        /// </summary>
        /// <param name="start">Start</param>
        /// <param name="count">Number of blocks</param>
        /// <param name="path">Path</param>
        [ConsoleCommand("export", "blocks", HelpCategory = "Blockchain Commands")]
        private void OnExportBlocksStartCountCommand(uint start, uint count, string path = null)
        {
            if (Blockchain.Singleton.Height < start) return;

            count = Math.Min(count, Blockchain.Singleton.Height - start + 1);

            if (string.IsNullOrEmpty(path))
            {
                path = $"chain.{start}.acc";
            }

            var writeStart = true;

            WriteBlocks(start, count, path, writeStart);
        }
    }
}
