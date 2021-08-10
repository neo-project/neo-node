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

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "install" command
        /// </summary>
        /// <param name="pluginName">Plugin name</param>
        [ConsoleCommand("install", Category = "Plugin Commands")]
        private void OnInstallCommand(string pluginName)
        {
            if (PluginExists(pluginName))
            {
                Console.WriteLine($"Plugin already exist.");
                return;
            }
            // To prevent circular dependency
            // put plugin-to-install into stack
            Stack<string> pluginToInstall = new();
            InstallPlugin(DownloadPlugin(pluginName), pluginName, pluginToInstall);
        }

        /// <summary>
        /// Force to install a pugin again. This will overwrite
        /// existing plugin files, in case of any file missing or
        /// damage to the old version.
        /// </summary>
        /// <param name="pluginName"></param>
        [ConsoleCommand("reinstall", Category = "Plugin Commands")]
        private void OnReinstallCommand(string pluginName)
        {
            if (PluginExists(pluginName))
            {
                Console.WriteLine($"Plugin already exist.");
                return;
            }
            Stack<string> pluginToInstall = new();
            InstallPlugin(DownloadPlugin(pluginName), pluginName, pluginToInstall, true);
        }

        /// <summary>
        /// Download plugin from github release
        /// The function of download and install are devided
        /// for the consideration of `update` command hat
        /// might be added in the future.
        /// </summary>
        /// <param name="pluginName">name of the plugin</param>
        /// <returns></returns>
        private MemoryStream DownloadPlugin(string pluginName)
        {
            HttpWebRequest request = WebRequest.CreateHttp($"https://github.com/neo-project/neo-modules/releases/download/v{typeof(Plugin).Assembly.GetVersion()}/{pluginName}.zip");
            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex) when (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
            {
                Version version_core = typeof(Plugin).Assembly.GetName().Version;
                request = WebRequest.CreateHttp($"https://api.github.com/repos/neo-project/neo-modules/releases");
                request.UserAgent = $"{GetType().Assembly.GetName().Name}/{GetType().Assembly.GetVersion()}";
                using HttpWebResponse response_api = (HttpWebResponse)request.GetResponse();
                using Stream stream = response_api.GetResponseStream();
                using StreamReader reader = new(stream);
                JObject releases = JObject.Parse(reader.ReadToEnd());
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
                request = WebRequest.CreateHttp(asset["browser_download_url"].GetString());
                response = (HttpWebResponse)request.GetResponse();
            }
            using (response)
            {
                var totalRead = 0L;
                byte[] buffer = new byte[1024];
                int read;

                using Stream stream = response.GetResponseStream();

                var output = new MemoryStream();
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    totalRead += read;
                    Console.Write($"\rDownloading {pluginName}.zip {totalRead / 1024}KB/{response.ContentLength / 1024}KB {(totalRead * 100) / response.ContentLength}%");
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
        private void InstallPlugin(MemoryStream stream, string pluginName, Stack<string> pluginToInstall, bool overWrite = false)
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
                InstallDependency(pluginName, pluginToInstall, zip);
                zip.ExtractToDirectory(".", overWrite);
                Console.WriteLine($"Install successful, please restart neo-cli.");
                pluginToInstall.Pop();
            }
            catch (IOException)
            {
                Console.WriteLine($"Plugin already exist.");
            };
        }

        /// <summary>
        /// Install the dependency of the plugin
        /// </summary>
        /// <param name="pluginName">plugin name</param>
        private void InstallDependency(string pluginName, Stack<string> pluginToInstall, ZipArchive zip)
        {
            try
            {
                ZipArchiveEntry entry = zip.GetEntry($"Plugins/{pluginName}/DEPENDENCY");
                using (StreamReader reader = new StreamReader(entry.Open()))
                {
                    Console.WriteLine("Installing dependencies.");
                    while (!reader.EndOfStream)
                    {
                        var plugin = reader.ReadLine();
                        if (plugin.Length == 0) continue;

                        if (PluginExists(plugin))
                        {
                            Console.WriteLine("Dependency already installed.");
                            continue;
                        }
                        InstallPlugin(DownloadPlugin(plugin), plugin, pluginToInstall);
                    }
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
                Console.WriteLine("Plugin not found");
                return;
            }
            if (plugin is Logger)
            {
                Console.WriteLine("You cannot uninstall a built-in plugin.");
                return;
            }

            DeleteFiles(plugin.Path,
                plugin.ConfigFile,
                $"{Plugin.PluginsDirectory}/{pluginName}/DEPENDENCY");

            try
            {
                Directory.Delete(Path.GetDirectoryName(plugin.ConfigFile), false);
            }
            catch (IOException)
            {
            }
            Console.WriteLine($"Uninstall successful, please restart neo-cli.");
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
                Console.WriteLine("No loaded plugins");
            }
        }
    }
}
