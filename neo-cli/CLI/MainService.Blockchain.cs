using Neo.ConsoleService;
using Neo.Ledger;
using System;
using System.ComponentModel;

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
        [Category("Blockchain Commands")]
        [ConsoleCommand("export", "blocks")]
        private void OnExportBlocksStartCountCommand(uint start, uint count = uint.MaxValue, string path = null)
        {
            if (Blockchain.Singleton.Height < start)
            {
                Console.WriteLine("error: invalid start height.");
                return;
            }

            count = Math.Min(count, Blockchain.Singleton.Height - start + 1);

            if (string.IsNullOrEmpty(path))
            {
                path = $"chain.{start}.acc";
            }

            WriteBlocks(start, count, path, true);
        }
    }
}
