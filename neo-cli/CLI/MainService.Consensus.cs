using Neo.ConsoleService;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "start consensus" command
        /// </summary>
        [ConsoleCommand("start consensus", Category = "Consensus Commands")]
        private void OnStartConsensusCommand()
        {
            if (NoWallet()) return;
            ShowPrompt = false;
            NeoSystem.StartConsensus(CurrentWallet);
        }
    }
}
