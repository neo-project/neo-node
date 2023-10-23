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
        /// Create a new script file from the template
        /// </summary>
        /// <param name="path">Path of the new script file</param>
        /// <example>new script script.cs</example>
        [ConsoleCommand("script new", Category = "Command Script")]
        private void OnCreatingNewScript(String path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = "script.cs";
            }

            if (!File.Exists(path))
            {
                ConsoleHelper.Info($"File {path} does not exist. Attempting to generate from template...");

                if (!File.Exists("template.cs"))
                {
                    ConsoleHelper.Error("Template file 'template.cs' does not exist. Unable to generate script.");
                    return;
                }

                File.Copy("template.cs", path);
                ConsoleHelper.Info($"File {path} generated from template.");
            }
            else
            {
                ConsoleHelper.Info($"File {path} already exists. No action taken.");
            }
        }

        /// <summary>
        /// Execute the script file
        /// </summary>
        /// <param name="path">path of the script file, script.cs if not given</param>
        /// <param name="watch"></param>
        /// <example>run script script.cs true</example>
        [ConsoleCommand("script run", Category = "Command Script")]
        private async Task OnExecutingScript(String path = null, bool watch = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = "script.cs";
            }

            if (!File.Exists(path))
            {
                ConsoleHelper.Error($"File {path} does not exist. Please create it using the 'new script' command.");
                return;
            }

            ConsoleHelper.Info("Executing script...");
            ConsoleHelper.Info("Path: " + path);

            // Execute the script once initially
            await ExecuteScript(path);

            if (watch)
            {
                // Use FileSystemWatcher to watch the file for changes
                using var watcher = new FileSystemWatcher();
                watcher.Path = Path.GetDirectoryName(path)!;
                watcher.Filter = Path.GetFileName(path);
                watcher.NotifyFilter = NotifyFilters.LastWrite;

                watcher.Changed += async (source, e) =>
                {
                    ConsoleHelper.Info($"File {e.FullPath} changed. Re-executing script...");
                    await ExecuteScript(path);
                };

                watcher.EnableRaisingEvents = true;

                ConsoleHelper.Info($"Watching for changes to {path}...");
                ConsoleHelper.Info("Press any key to stop watching...");
                Console.ReadKey();
            }
        }

        private async Task ExecuteScript(string path)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await CSharpScript.EvaluateAsync(
                        await File.ReadAllTextAsync(path),
                        ScriptOptions.Default.WithImports("System", "System.Threading", "System.Linq").WithReferences(typeof(NeoSystem).Assembly, typeof(ScriptHelper.ScriptHelper).Assembly),
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
