using Akka.Actor;
using Neo.CommandParser;
using Neo.Consensus;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "change view" command
        /// </summary>
        [ConsoleCommand("change", "view", HelpCategory = "Consensus Commands")]
        private void OnChangeViewCommand(byte viewnumber)
        {
            NeoSystem.Consensus?.Tell(new ConsensusService.SetViewNumber { ViewNumber = viewnumber });
        }

        /// <summary>
        /// Process "start consensus" command
        /// </summary>
        [ConsoleCommand("start", "consensus", HelpCategory = "Consensus Commands")]
        private void OnStartConsensusCommand()
        {
            if (NoWallet()) return;
            ShowPrompt = false;
            NeoSystem.StartConsensus(CurrentWallet);
        }
    }
}
