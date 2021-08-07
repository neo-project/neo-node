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
            CheckAndInstall(pluginName);
        }

        private void CheckAndInstall(string pluginName)
        {
            if (pluginName == "ApplicationLogs" ||
            pluginName == "RpcNep17Tracker" ||
            pluginName == "StateService" ||
            pluginName == "OracleService"
            )
            {
                if (!Directory.Exists("./Plugins/RpcServer") || !File.Exists("./Plugins/RpcServer.dll"))
                {
                    Console.WriteLine($"Installing plugin and dependencies.");
                    InstallPlugin("RpcServer");
                }
            }
            InstallPlugin(pluginName);
        }

        private void InstallPlugin(string pluginName)
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
                const int IO_BUFFER_SIZE = 16 * 1024;

                var totalRead = 0L;
                byte[] buffer = new byte[IO_BUFFER_SIZE];
                int read;

                using Stream stream = response.GetResponseStream();

                using var output = new MemoryStream();
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    totalRead += read;
                    Console.Write($"\rDowloading {pluginName}.zip   {totalRead / 1024}KB/{response.ContentLength / 1024}KB  {(totalRead * 100) / response.ContentLength}%");
                }
                Console.WriteLine();
                using (SHA256 sha256 = SHA256.Create())
                {
                    Console.WriteLine("SHA256: " + sha256.ComputeHash(output.ToArray()).ToHexString().ToString());
                }

                using ZipArchive zip = new(output, ZipArchiveMode.Read);
                try
                {
                    zip.ExtractToDirectory(".");
                    Console.WriteLine($"Install successful, please restart neo-cli.");
                }
                catch (IOException)
                {
                    Console.WriteLine($"Plugin already exist.");
                };

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
                    Console.WriteLine($"\t{plugin.Name,-20}{plugin.Description}");
                }
            }
            else
            {
                Console.WriteLine("No loaded plugins");
            }
        }
    }
}
