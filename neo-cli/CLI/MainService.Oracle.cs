using Neo.ConsoleService;
using Neo.SmartContract;
using System;

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

            // TODO: Remove this before merge to master

            ApplicationEngine.Log += ApplicationEngine_Log;
        }

        private void ApplicationEngine_Log(object sender, LogEventArgs e)
        {
            var cl = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("log " + e.ScriptHash.ToString() + ":" + e.Message);
            Console.ForegroundColor = cl;
        }
    }
}
