using Neo.ConsoleService;
using Neo.SmartContract.Native;
using System;
using System.Linq;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "list nativecontract" command
        /// </summary>
        [ConsoleCommand("list nativecontract", Category = "Native Contract")]
        private void OnListNativeContract()
        {
            NativeContract.Contracts.ToList().ForEach(p => Console.WriteLine("\t" + p.Name + "\t" + p.Hash));
        }
    }
}
