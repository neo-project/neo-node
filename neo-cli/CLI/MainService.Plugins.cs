// Copyright (C) 2016-2021 The Neo Project.
// 
// The neo-cli is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
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
            if (PluginExists(pluginName))
            {
                Console.WriteLine($"Plugin already exist.");
                return;
            }
            // To prevent circular dependency
            // put plugin-to-install into stack
            Stack<string> pluginToInstall = new();
            await InstallPlugin(await DownloadPlugin(pluginName), pluginName, pluginToInstall);
        }

        /// <summary>
        /// Force to install a plugin again. This will overwrite
        /// existing plugin files, in case of any file missing or
        /// damage to the old version.
        /// </summary>
        /// <param name="pluginName">name of the plugin</param>
        [ConsoleCommand("reinstall", Category = "Plugin Commands", Description = "Overwrite existing plugin by force.")]
        private async Task OnReinstallCommand(string pluginName)
        {
            Stack<string> pluginToInstall = new();
            await InstallPlugin(await DownloadPlugin(pluginName), pluginName, pluginToInstall, true);
        }

        /// <summary>
        /// Download plugin from github release
        /// The function of download and install are devided
        /// for the consideration of `update` command that
        /// might be added in the future.
        /// </summary>
        /// <param name="pluginName">name of the plugin</param>
        /// <returns>Downloaded content</returns>
        private async Task<MemoryStream> DownloadPlugin(string pluginName)
        {
            var url = $"https: //github.com/neo-project/neo-modules/releases/download/v{typeof(Plugin).Assembly.GetVersion()}/{pluginName}.zip";
            using HttpClient http = new();
            HttpResponseMessage response = await http.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                Version versionCore = typeof(Plugin).Assembly.GetName().Version;
                HttpRequestMessage request = new(HttpMethod.Get, "https://api.github.com/repos/neo-project/neo-modules/releases");
                request.Headers.UserAgent.ParseAdd($"{GetType().Assembly.GetName().Name}/{GetType().Assembly.GetVersion()}");
                using HttpResponseMessage responseApi = await http.SendAsync(request);
                byte[] buffer = await responseApi.Content.ReadAsByteArrayAsync();
                JObject releases = JObject.Parse(buffer);
                JObject asset = releases.GetArray()
                    .Where(p => !p["tag_name"].GetString().Contains('-'))
                    .Select(p => new
                    {
                        Version = Version.Parse(p["tag_name"].GetString().TrimStart('v')),
                        Assets = p["assets"].GetArray()
                    })
                    .OrderByDescending(p => p.Version)
                    .First(p => p.Version <= versionCore).Assets
                    .FirstOrDefault(p => p["name"].GetString() == $"{pluginName}.zip");
                if (asset is null) throw new Exception("Plugin doesn't exist.");
                response = await http.GetAsync(asset["browser_download_url"].GetString());
            }

            using (response)
            {
                var totalRead = 0L;
                byte[] buffer = new byte[1024];
                int read;

                await using Stream stream = await response.Content.ReadAsStreamAsync();
                Console.WriteLine($"From {url}");
                var output = new MemoryStream();
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    totalRead += read;
                    Console.Write($"\rDownloading {pluginName}.zip {totalRead / 1024}KB/{response.Content.Headers.ContentLength / 1024}KB {(totalRead * 100) / response.Content.Headers.ContentLength}%");
                }
                Console.WriteLine();

                return output;
            }
        }

        /// <summary>
        /// Install plugin from stream
        /// </summary>
        /// <param name="stream">stream of the plugin</param>
        /// <param name="pluginName">name of the plugin</param>
        /// <param name="pluginToInstall">installing plugin stack</param>
        /// <param name="overWrite">Install by force for `update`</param>
        private async Task InstallPlugin(MemoryStream stream, string pluginName, Stack<string> pluginToInstall, bool overWrite = false)
        {
            // If plugin already in the installing stack,
            // It means there has circular dependency.
            // Throw exception.
            if (pluginToInstall.Contains(pluginName))
            {
                Console.WriteLine("Plugin has circular dependency");
                throw new DuplicateWaitObjectException();
            }
            pluginToInstall.Push(pluginName);

            using (SHA256 sha256 = SHA256.Create())
            {
                Console.WriteLine("SHA256: " + sha256.ComputeHash(stream.ToArray()).ToHexString().ToString());
            }
            using ZipArchive zip = new(stream, ZipArchiveMode.Read);

            try
            {
                zip.ExtractToDirectory(".", overWrite);
                await InstallDependency(pluginName, pluginToInstall);
                Console.WriteLine($"Install successful, please restart neo-cli.");
                pluginToInstall.Pop();
            }
            catch (IOException)
            {
                pluginToInstall.Pop();
                Console.WriteLine($"Plugin already exist. Try to run `reinstall {pluginName}`");
            }
            ;
        }

        /// <summary>
        /// Install the dependency of the plugin
        /// </summary>
        /// <param name="pluginName">plugin name</param>
        /// <param name="pluginToInstall">installing plugin stack</param>
        private async Task InstallDependency(string pluginName, Stack<string> pluginToInstall)
        {
            try
            {
                IConfigurationSection dependency = new ConfigurationBuilder()
                    .AddJsonFile($"Plugins/{pluginName}/config.json", optional: true)
                    .Build()
                    .GetSection("Dependency");

                var dependencies = dependency.GetChildren().Select(p => p.Get<string>()).ToArray();

                if (dependencies.Length == 0) return;

                Console.WriteLine("Installing dependencies.");
                foreach (string plugin in dependencies)
                {
                    if (plugin.Length == 0) continue;

                    if (PluginExists(plugin))
                    {
                        Console.WriteLine("Dependency already installed.");
                        continue;
                    }
                    await InstallPlugin(await DownloadPlugin(plugin), plugin, pluginToInstall);
                }
            }
            catch
            {
                // Fail to read DEPENDENCY means there is no dependency
                return;
            }
        }

        private bool PluginExists(string pluginName)
        {
            return File.Exists($"{Plugin.PluginsDirectory}/{pluginName}.dll");
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

            DeleteFiles(plugin.Path,
                plugin.ConfigFile);

            try
            {
                Directory.Delete(Path.GetDirectoryName(plugin.ConfigFile), false);
            }
            catch (IOException)
            {
            }
            ConsoleHelper.Info("Uninstall successful, please restart neo-cli.");
        }

        private void DeleteFiles(params string[] list)
        {
            foreach (var file in list)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
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
                    var name = $"{plugin.Name}@{plugin.Version}";
                    Console.WriteLine($"\t{name,-25}{plugin.Description}");
                }
            }
            else
            {
                ConsoleHelper.Warning("No loaded plugins");
            }
        }
    }
}
