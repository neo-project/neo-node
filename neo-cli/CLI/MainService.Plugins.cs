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
using System.Net.Http;
using System.Threading.Tasks;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "install" command
        /// </summary>
        /// <param name="pluginName">Plugin name</param>
        [ConsoleCommand("install", Category = "Plugin Commands")]
        private async Task OnInstallCommandAsync(string pluginName)
        {
            using HttpClient http = new();
            HttpResponseMessage response = await http.GetAsync($"https://github.com/neo-project/neo-modules/releases/download/v{typeof(Plugin).Assembly.GetVersion()}/{pluginName}.zip");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                Version version_core = typeof(Plugin).Assembly.GetName().Version;
                HttpRequestMessage request = new(HttpMethod.Get, "https://api.github.com/repos/neo-project/neo-modules/releases");
                request.Headers.UserAgent.ParseAdd($"{GetType().Assembly.GetName().Name}/{GetType().Assembly.GetVersion()}");
                using HttpResponseMessage response_api = await http.SendAsync(request);
                byte[] buffer = await response_api.Content.ReadAsByteArrayAsync();
                JObject releases = JObject.Parse(buffer);
                JObject asset = releases.GetArray()
                    .Where(p => !p["tag_name"].GetString().Contains('-'))
                    .Select(p => new
                    {
                        Version = Version.Parse(p["tag_name"].GetString().TrimStart('v')),
                        Assets = p["assets"].GetArray()
                    })
                    .OrderByDescending(p => p.Version)
                    .First(p => p.Version <= version_core).Assets
                    .FirstOrDefault(p => p["name"].GetString() == $"{pluginName}.zip");
                if (asset is null) throw new Exception("Plugin doesn't exist.");
                response = await http.GetAsync(asset["browser_download_url"].GetString());
            }
            using (response)
            {
                using Stream stream = await response.Content.ReadAsStreamAsync();
                using ZipArchive zip = new(stream, ZipArchiveMode.Read);
                try
                {
                    zip.ExtractToDirectory(".");
                    ConsoleHelper.Info("Install successful, please restart neo-cli.");
                }
                catch (IOException)
                {
                    ConsoleHelper.Warning($"Plugin already exist.");
                }
            }
        }

        /// <summary>
        /// Process "uninstall" command
        /// </summary>
        /// <param name="pluginName">Plugin name</param>
        [ConsoleCommand("uninstall", Category = "Plugin Commands")]
        private void OnUnInstallCommand(string pluginName)
        {
            var plugin = Plugin.Plugins.FirstOrDefault(p => p.Name == pluginName);
            if (plugin is null)
            {
                ConsoleHelper.Warning("Plugin not found");
                return;
            }
            if (plugin is Logger)
            {
                ConsoleHelper.Warning("You cannot uninstall a built-in plugin.");
                return;
            }

            File.Delete(plugin.Path);
            File.Delete(plugin.ConfigFile);
            try
            {
                Directory.Delete(Path.GetDirectoryName(plugin.ConfigFile), false);
            }
            catch (IOException)
            {
            }
            ConsoleHelper.Info("Uninstall successful, please restart neo-cli.");
        }

        /// <summary>
        /// Process "plugins" command
        /// </summary>
        [ConsoleCommand("plugins", Category = "Plugin Commands")]
        private void OnPluginsCommand()
        {
            if (Plugin.Plugins.Count > 0)
            {
                Console.WriteLine("Loaded plugins:");
                foreach (Plugin plugin in Plugin.Plugins)
                {
                    if (plugin is Logger) continue;
                    ConsoleHelper.Info($"\t{plugin.Name,-20}", plugin.Description);
                }
            }
            else
            {
                ConsoleHelper.Warning("No loaded plugins");
            }
        }
    }
}
