using Neo.CommandParser;
using System.ComponentModel;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "start consensus" command
        /// </summary>
        [Category("Consensus Commands")]
        [ConsoleCommand("start", "consensus")]
        private void OnStartConsensusCommand()
        {
            if (NoWallet()) return;
            ShowPrompt = false;
            NeoSystem.StartConsensus(CurrentWallet);
        }
    }
}
