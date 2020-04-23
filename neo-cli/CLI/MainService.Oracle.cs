using Neo.ConsoleService;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "start oracle" command
        /// </summary>
        [ConsoleCommand("start oracle", Category = "Oracle Commands")]
        private void OnStartOracleCommand(byte numberOfTasks = 4)
        {
            if (NoWallet()) return;
            ShowPrompt = false;
            NeoSystem.StartOracle(CurrentWallet, numberOfTasks);
        }

        /// <summary>
        /// Process "stop oracle" command
        /// </summary>
        [ConsoleCommand("stop oracle", Category = "Oracle Commands")]
        private void OnStopOracleCommand()
        {
            if (!NeoSystem.StopOracle()) return;
            ShowPrompt = true;
        }
    }
}
