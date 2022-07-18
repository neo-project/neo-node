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
using System.Threading.Tasks;
using Akka.Util.Internal;
namespace Neo.CLI;

partial class MainService
{
    /// <summary>
    /// Process "mode list" command.
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
            MoveModeConfig(modeName.ToLower());
            // foreach (var file in dir.GetFiles().Where(p => p.Extension == ".json"))
            // {
            //     var targetFilePath = Path.Combine(targetMode.FullName, file.Name);
            //     file.CopyTo(targetFilePath, true);
            // }
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
            var dir = new DirectoryInfo($"Modes/{modeName.ToLower()}");
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

    /// <summary>
    /// Load the target mode, plugin should be according to the mode,
    /// if the mode contains the plugin, install the plugin, otherwise delete the plugin
    /// </summary>
    /// <param name="mode"> name of the mode</param>
    /// <exception cref="DirectoryNotFoundException"> if the mode is not found</exception>
    private Task LoadMode(string mode)
    {
        try
        {
            var dir = new DirectoryInfo($"./Modes/{mode.ToLower()}");
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Mode not found: {dir.FullName}");

            // Process the plugin
            var modePlugins = dir.GetFiles();

            modePlugins.ForEach(async p =>
            {
                // if the plugin does not exist, maybe consider install it
                if (!Directory.Exists($"Plugins/{p.Name}/"))
                {
                     await InstallPluginAsync(p.Name);
                }
                File.Copy($"Modes/{mode.ToLower()}/{p.Name}.json",
                    $"Plugins/{p.Name}/config.json", true);
            });
            MoveModeConfig(mode.ToLower(), false);

            // Get existing plugins and delete them if they are not in the mode
            new DirectoryInfo("Plugins/").GetDirectories().ForEach(p =>
            {
                if (modePlugins.Any(k => k.Name == p.Name)) return;
                if(!File.Exists($"Plugins/{p.Name}/config.json")) return;
                try
                {
                    ConsoleHelper.Info("Removing plugin ", p.Name);
                    Directory.Delete($"Plugins/{p.Name}", true);
                }
                catch
                {
                    // ignored
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        return Task.CompletedTask;
    }

    /// save config.json and config.fs.json to the mode directory
    /// <param name="mode"> name of the mode</param>
    /// <param name="toMode"></param>
    /// <exception cref="DirectoryNotFoundException"> if the mode is not found</exception>
    private static void MoveModeConfig(string mode, bool toMode=true)
    {
        var modeDir = new DirectoryInfo($"./Modes/{mode.ToLower()}");
        var configDir = new DirectoryInfo("./");
        if (!modeDir.Exists)
            throw new DirectoryNotFoundException($"Mode not found: {modeDir.FullName}");
        try
        {
            if (toMode)
            {
                File.Copy(configDir.FullName + "/config.json",
                    modeDir.FullName + "/config.json", true);
                File.Copy(configDir.FullName + "/config.fs.json",
                    modeDir.FullName + "/config.fs.json", true);
            }
            else
            {
                File.Copy(modeDir.FullName + "/config.json",
                    configDir.FullName + "/config.json", true);
                File.Copy(modeDir.FullName + "/config.fs.json",
                    configDir.FullName + "/config.fs.json", true);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
