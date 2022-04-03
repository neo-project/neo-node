// Copyright (C) 2016-2021 The Neo Project.
//
// The neo-cli is free software distributed under the MIT software
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Plugins;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
namespace Neo.CLI;

 partial class MainService
    {
        /// <summary>
        /// Process "install" command
        /// </summary>
        /// <param name="modeName">Mode name</param>
        [ConsoleCommand("mode load", Category = "Mode Commands")]
        private async Task OnLoadMode(string modeName)
        {


            // using (response)
            // {
            //     await using Stream stream = await response.Content.ReadAsStreamAsync();
            //     using ZipArchive zip = new(stream, ZipArchiveMode.Read);
            //     try
            //     {
            //         zip.ExtractToDirectory(".");
            //     }
            //     catch (IOException)
            //     {
            //     }
            // }
        }

        /// <summary>
        /// Process "mode list" command
        /// <param name="modeName">Mode name</param>
        /// </summary>
        [ConsoleCommand("mode list", Category = "Mode Commands")]
        private void OnListModes()
        {
            // var plugin = Plugin.Plugins.FirstOrDefault(p => p.Name == pluginName);
            // if (plugin is null)
            // {
            //     ConsoleHelper.Warning("Plugin not found");
            //     return;
            // }
            // if (plugin is Logger)
            // {
            //     ConsoleHelper.Warning("You cannot uninstall a built-in plugin.");
            //     return;
            // }

            // File.Delete(plugin.Path);
            // File.Delete(plugin.ConfigFile);
            // try
            // {
            //     Directory.Delete(Path.GetDirectoryName(plugin.ConfigFile), false);
            // }
            // catch (IOException)
            // {
            // }
            // ConsoleHelper.Info("Uninstall successful, please restart neo-cli.");
        }

        /// <summary>
        /// Process "mode save" command
        /// <param name="modeName">Mode name</param>
        /// </summary>
        [ConsoleCommand("mode save", Category = "Mode Commands")]
        private void OnSaveMode(string modeName)
        {
            // if (Plugin.Plugins.Count > 0)
            // {
            //     Console.WriteLine("Loaded plugins:");
            //     foreach (Plugin plugin in Plugin.Plugins)
            //     {
            //         if (plugin is Logger) continue;
            //         ConsoleHelper.Info($"\t{plugin.Name,-20}", plugin.Description);
            //     }
            // }
            // else
            // {
            //     ConsoleHelper.Warning("No loaded plugins");
            // }
        }

        /// <summary>
        /// Process "plugins" command
        /// <param name="modeName">Mode name</param>
        /// </summary>
        [ConsoleCommand("mode delete", Category = "Mode Commands")]
        private void OnDeleteMode(string modeName)
        {
            // if (Plugin.Plugins.Count > 0)
            // {
            //     Console.WriteLine("Loaded plugins:");
            //     foreach (Plugin plugin in Plugin.Plugins)
            //     {
            //         if (plugin is Logger) continue;
            //         ConsoleHelper.Info($"\t{plugin.Name,-20}", plugin.Description);
            //     }
            // }
            // else
            // {
            //     ConsoleHelper.Warning("No loaded plugins");
            // }
        }
    }
