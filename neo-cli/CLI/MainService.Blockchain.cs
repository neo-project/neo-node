using Neo.ConsoleService;
using Neo.Ledger;
using Neo.SmartContract.Native;
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
        [ConsoleCommand("export blocks", Category = "Blockchain Commands")]
        private void OnExportBlocksStartCountCommand(uint start, uint count = uint.MaxValue, string path = null)
        {
            uint height = NativeContract.Ledger.CurrentIndex(Blockchain.Singleton.View);
            if (height < start)
            {
                Console.WriteLine("Error: invalid start height.");
                return;
            }

            count = Math.Min(count, height - start + 1);

            if (string.IsNullOrEmpty(path))
            {
                path = $"chain.{start}.acc";
            }

            WriteBlocks(start, count, path, true);
        }
    }
}
