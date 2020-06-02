using Akka.Util.Internal;
using Neo.ConsoleService;
using Neo.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

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
            bool isTemp;
            string fileName;

            if (!File.Exists(pluginName))
            {
                if (string.IsNullOrEmpty(Settings.Default.PluginURL))
                {
                    Console.WriteLine("You must define `PluginURL` in your `config.json`");
                    return;
                }

                var address = string.Format(Settings.Default.PluginURL, pluginName, typeof(Plugin).Assembly.GetVersion());
                fileName = Path.Combine(Path.GetTempPath(), $"{pluginName}.zip");
                isTemp = true;

                Console.WriteLine($"Downloading from {address}");
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(address, fileName);
                }
            }
            else
            {
                fileName = pluginName;
                isTemp = false;
            }

            try
            {
                ZipFile.ExtractToDirectory(fileName, ".");
            }
            catch (IOException)
            {
                Console.WriteLine($"Plugin already exist.");
                return;
            }
            finally
            {
                if (isTemp)
                {
                    File.Delete(fileName);
                }
            }

            Console.WriteLine($"Install successful, please restart neo-cli.");
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
            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("ApplicationLogs", "Synchronizes the smart contract log with the NativeContract log (Notify)");
            dic.Add("LevelDBStore", "Uses LevelDB to store the blockchain data");
            dic.Add("RocksDBStore", "Uses RocksDBStore to store the blockchain data");
            dic.Add("RpcNep5Tracker", "Enquiries NEP-5 balance and transactions history of accounts through RPC");
            dic.Add("RpcServer", "Enables RPC for the node");
            dic.Add("StatesDumper", "Exports Neo-CLI status data");
            dic.Add("SystemLog", "Prints the consensus log");

            if (Plugin.Plugins.Count > 0)
            {
                Console.WriteLine("Installed plugins:");
                Plugin.Plugins.ForEach(p =>
                    {
                        var description = dic.GetValueOrDefault(p.Name, "Description");
                        Console.WriteLine("\t" + p.Name + "\t" + description);
                        dic.Remove(p.Name);
                    });
                Console.WriteLine("Uninstalled plugins:");
                dic.ForEach(p => Console.WriteLine("\t" + p.Key + "\t" + p.Value));
            }
            else
            {
                Console.WriteLine("No installed plugins");
            }
        }
    }
}
