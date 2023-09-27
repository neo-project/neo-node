// Copyright (C) 2016-2023 The Neo Project.
//
// The neo-cli is free software distributed under the MIT software
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Neo.ConsoleService;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neo.SmartContract.Native;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "list nativecontract" command
        /// </summary>
        [ConsoleCommand("run script", Category = "Native Contract")]
        private async Task OnExecutingScript(String path = null)
        {
            if (path == null)
            {
                ConsoleHelper.Error("Please specify a path to a script file.");
                return;
            }
            ConsoleHelper.Info("Executing script...");
            ConsoleHelper.Info("Path: " + path);

            await Task.Run(async () =>
            {
                try
                {
                    await CSharpScript.EvaluateAsync(
                        await File.ReadAllTextAsync(path),
                        ScriptOptions.Default.WithImports("System", "System.Threading", "System.Linq").WithReferences(typeof(NeoSystem).Assembly),
                        globals: this
                    );
                    ConsoleHelper.Info("Result: " + NativeContract.Contracts.ToList().Count);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"{e}");
                }
            });
        }
    }
}
