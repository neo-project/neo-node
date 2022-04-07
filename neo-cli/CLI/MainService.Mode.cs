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
using System.Text;
using System.Threading.Tasks;
using Akka.Util.Internal;
namespace Neo.CLI;

 partial class MainService
    {
        /// <summary>
        /// Process "mode list" command
        /// <param name="modeName">Mode name</param>
        /// </summary>
        [ConsoleCommand("mode list", Category = "Mode Commands")]
        private void OnListModes()
        {
            try
            {
                Directory.GetDirectories("./Modes/").ForEach(p => ConsoleHelper.Info(p));
            }
            catch (IOException)
            {
            }
        }

        /// <summary>
        /// Process "mode save" command
        /// <param name="modeName">Mode name</param>
        /// </summary>
        [ConsoleCommand("mode save", Category = "Mode Commands")]
        private void OnSaveMode(string modeName)
        {

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



        private void LoadConfig(string mode)
        {
            var dir = new DirectoryInfo(mode);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Mode not found: {dir.FullName}");

            // Cache directories before we start copying
            var dirs = dir.GetDirectories();

            // Get the config files of the node
            foreach (var file in dir.GetFiles())
            {
                var targetFilePath = Path.Combine("./", file.Name);
                file.CopyTo(targetFilePath, true);
            }

            // Copy the Plugin files
            foreach (var plugin in dirs)
            {
                foreach (var file in plugin.GetFiles())
                {
                    var targetFilePath = Path.Combine($"Plugins/{plugin.Name}", file.Name);
                    file.CopyTo(targetFilePath, true);
                }
            }
        }

        private void SaveMode(string mode)
        {
            string path = @"./Modes/Mode";

            try
            {
                // if(File.Exists(path)) File.Delete(path);
                using (var fs = File.Create(path))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes(mode);
                    fs.Write(info, 0, info.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            // using (StreamReader sr = File.OpenText(path))
            // {
            //     string s = "";
            //     while ((s = sr.ReadLine()) != null)
            //     {
            //         Console.WriteLine(s);
            //     }
            // }
        }
    }
