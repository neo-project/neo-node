using Neo.CLI.CommandParser;
using Neo.Ledger;
using Neo.Services;
using System;

namespace Neo.CLI
{
    public partial class MainService : ConsoleServiceBase
    {
        /// <summary>
        /// Process "export block" command
        /// Process "export blocks" command
        /// </summary>
        [ConsoleCommand("export", "block", HelpCategory = "Blockchain Commands")]
        [ConsoleCommand("export", "blocks", HelpCategory = "Blockchain Commands")]
        private void OnExportBlocksCommand(uint start, uint count = uint.MaxValue)
        {
            if (Blockchain.Singleton.Height < start) return;

            count = Math.Min(count, Blockchain.Singleton.Height - start + 1);
            var path = $"chain.{start}.acc";
            var writeStart = true;

            WriteBlocks(start, count, path, writeStart);
        }

        /// <summary>
        /// Process "export block" command
        /// Process "export blocks" command
        /// </summary>
        [ConsoleCommand("export", "block", HelpCategory = "Blockchain Commands")]
        [ConsoleCommand("export", "blocks", HelpCategory = "Blockchain Commands")]
        private void OnExportBlocksCommand(string path, uint start)
        {
            var writeStart = false;
            var count = Blockchain.Singleton.Height - start + 1;
            path = string.IsNullOrEmpty(path) ? "chain.acc" : path;

            WriteBlocks(start, count, path, writeStart);
        }
    }
}
