using Neo.ConsoleService;
using Neo.IO.Json;
using Neo.Plugins;
using System;
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
            if (Directory.Exists($"{Plugin.PluginsDirectory}/{pluginName}") &&
                File.Exists($"{Plugin.PluginsDirectory}/{pluginName}.dll"))
            {
                Console.WriteLine($"Plugin already exist.");
                return;
            }
            InstallPlugin(DownloadPlugin(pluginName), pluginName);
        }


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


        private void InstallPlugin(MemoryStream stream, string pluginName)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                Console.WriteLine("SHA256: " + sha256.ComputeHash(stream.ToArray()).ToHexString().ToString());
            }

            using ZipArchive zip = new(stream, ZipArchiveMode.Read);

            try
            {
                zip.ExtractToDirectory(".");
                InstallDependency(pluginName);
                Console.WriteLine($"Install successful, please restart neo-cli.");
            }
            catch (IOException)
            {
                Console.WriteLine($"Plugin already exist.");
            };
        }

        private void InstallDependency(string pluginName)
        {
            try
            {
                string[] plugins = File.ReadAllLines($"{Plugin.PluginsDirectory}/{pluginName}/DEPENDENCY");
                foreach (string plugin in plugins)
                {
                    if (plugin.Length == 0) continue;

                    if (Directory.Exists($"{Plugin.PluginsDirectory}/{pluginName}") &&
                        File.Exists($"{Plugin.PluginsDirectory}/{pluginName}.dll"))
                    {
                        continue;
                    }

                    InstallPlugin(DownloadPlugin(plugin), plugin);
                }
            }
            catch
            {
                // Fail to read DEPENDENCY means there is no dependency
                return;
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
                Console.WriteLine("Plugin not found");
                return;
            }
            if (plugin is Logger)
            {
                Console.WriteLine("You cannot uninstall a built-in plugin.");
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
            Console.WriteLine($"Uninstall successful, please restart neo-cli.");
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
