// Copyright (C) 2016-2022 The Neo Project.
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
        // if no mode name assigned
        if (modeName is null)
        {
            ConsoleHelper.Error("No mode name assigned.");
            return;
        }
        modeName = modeName.ToLower();
        try
        {
            var dir = new DirectoryInfo($"./");

            var targetMode = new DirectoryInfo($"Modes/{modeName}");
            // Create the mode if it does not exist
            if (!targetMode.Exists) Directory.CreateDirectory(targetMode.FullName);

            // Get the config files of the node
            foreach (var file in dir.GetFiles().Where(p => p.Extension == ".json"))
            {
                var targetFilePath = Path.Combine(targetMode.FullName, file.Name);
                file.CopyTo(targetFilePath, true);
            }
            var plugins = new DirectoryInfo("./Plugins");
            // Cache directories before we start copying
            var dirs = plugins.GetDirectories();
            // Save the Plugin files
            foreach (var plugin in dirs)
            {
                foreach (var file in plugin.GetFiles().Where(p => p.Extension == ".json"))
                {
                    file.CopyTo($"Modes/{modeName}/{plugin.Name}.json", true);
                }
            }
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
            Directory.Delete(dir.FullName, true);
            ConsoleHelper.Info("Mode ", modeName, " was deleted.");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static void LoadMode(string mode)
    {
        try
        {
            var dir = new DirectoryInfo($"./Modes/{mode}");
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Mode not found: {dir.FullName}");

            // Get the config files of the node
            foreach (var file in dir.GetFiles())
            {
                if (file.Name is "config.json" or "config.fs.json")
                {
                    var targetFilePath = Path.Combine("./", file.Name);
                    file.CopyTo(targetFilePath, true);
                }
                else
                {
                    var plugin = file.Name.Split('.')[0];
                    // if the plugin no longer exists, just ignore it.
                    if (!Directory.Exists($"Plugins/{plugin}/")) continue;
                    file.CopyTo($"Plugins/{plugin}/config.json", true);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
