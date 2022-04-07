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
using System;
using System.IO;
using System.Linq;
using System.Text;
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
        // if no mode name assigned, save on current mode
        modeName ??= LoadCurrentMode();
        modeName = modeName.ToLower();
        try
        {
            var dir = new DirectoryInfo($"./");

            // Get the config files of the node
            foreach (var file in dir.GetFiles().Where(p => p.Extension == ".json"))
            {
                var targetMode = new DirectoryInfo($"Modes/{modeName}");
                // Create the mode if it does not exist
                if (!targetMode.Exists) Directory.CreateDirectory(targetMode.FullName);
                var targetFilePath = Path.Combine(targetMode.FullName, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            var plugins = new DirectoryInfo($"./Plugins");
            // Cache directories before we start copying
            var dirs = plugins.GetDirectories();

            // Save the Plugin files
            foreach (var plugin in dirs)
            {
                foreach (var file in plugin.GetFiles().Where(p => p.Extension == ".json"))
                {
                    var targetPlugin = new DirectoryInfo($"Modes/{modeName}/{plugin.Name}");
                    if (!targetPlugin.Exists) Directory.CreateDirectory(targetPlugin.FullName);
                    var targetFilePath = Path.Combine(targetPlugin.FullName, file.Name);
                    file.CopyTo(targetFilePath, true);
                }
            }
            // Update the most recent mode
            SaveMode(modeName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

    }

    /// <summary>
    /// Process "mode delete" command
    /// <param name="modeName">Mode name</param>
    /// </summary>
    [ConsoleCommand("mode delete", Category = "Mode Commands")]
    private void OnDeleteMode(string modeName)
    {
        try
        {
            var dir = new DirectoryInfo($"Modes/{modeName}");
            if (!dir.Exists)
                return;
            Directory.Delete(dir.FullName);
            ConsoleHelper.Info("Mode ", modeName, " was deleted.");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static void LoadMode()
    {
        try
        {
            var mode = LoadCurrentMode();
            var dir = new DirectoryInfo($"./Modes/{mode}");
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
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static void SaveMode(string mode)
    {
        const string path = @"./Modes/Mode";
        mode ??= "mainnet";
        mode = mode.ToLower();
        try
        {
            // if(File.Exists(path)) File.Delete(path);
            using var fs = File.Create(path);
            var info = new UTF8Encoding(true).GetBytes(mode);
            fs.Write(info, 0, info.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static string LoadCurrentMode()
    {
        const string path = @"./Modes/Mode";
        var mode = "mainnet";
        try
        {
            using var sr = File.OpenText(path);
            // If there is no valid mode, load mainnet as default.
            mode = sr.ReadLine() ?? mode;
        }
        catch (Exception)
        {
            // ignored
            ConsoleHelper.Error("Mode system is crashed, please reinstall the node");
            /// TODO: Maybe allow users to fix it automatically
        }
        return mode.ToLower();
    }
}
