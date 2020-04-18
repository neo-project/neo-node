using Neo.ConsoleService;
using Neo.Oracle;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
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

            ApplicationEngine.Log += ApplicationEngine_Log;
        }

        private void ApplicationEngine_Log(object sender, LogEventArgs e)
        {
            Console.WriteLine("LOG:" + e.Message);
        }

        // TODO: Remove this test before merge to master

        /// <summary>
        /// Process "oracle test" command
        /// </summary>
        [ConsoleCommand("oracle test", Category = "Oracle Commands")]
        private void OnOracleTest(string url, OracleWalletBehaviour type = OracleWalletBehaviour.OracleWithoutAssert)
        {
            if (NoWallet()) return;

            using ScriptBuilder script = new ScriptBuilder();

            script.EmitAppCall(NativeContract.Oracle.Hash, "get", url, null, null, null);
            script.EmitSysCall(InteropService.Runtime.Log);

            var tx = CurrentWallet.MakeTransaction(script.ToArray(), sender: null, attributes: null, cosigners: null, oracle: type);

            SignAndSendTx(tx);
        }
    }
}
