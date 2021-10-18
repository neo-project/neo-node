// Copyright (C) 2021 The Neo Project.
// 
// The upgrader is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace upgrader
{
    class Program
    {
        private static string Temp => Path.GetTempPath();
        static void Main(string[] args)
        {
            var pluginPath = "Plugins";
            if (!Directory.Exists(pluginPath)) return;
            var pluginEntries = Directory.GetFiles(pluginPath, "*.dll").Select(file => Path.GetFileNameWithoutExtension(file)).ToList();
            GetLatestVersion(pluginEntries);
        }


        /// <summary>
        /// Download the most lattest file
        /// </summary>
        /// <param name="dllFiles">The dll file in the local `Plugins` folder</param>
        private static void GetLatestVersion(List<string> dllFiles)
        {
            HttpWebRequest request = WebRequest.CreateHttp($"https://api.github.com/repos/neo-project/neo-modules/releases/latest");
            request.UserAgent = "Foo";
            request.Accept = "application/json";
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using StreamReader stream = new StreamReader(response.GetResponseStream());
                string json = stream.ReadToEnd();
                var objects = JObject.Parse(json); // parse as array
                var version = objects["name"].GetString();
                var assets = objects["assets"].GetArray();

                // Update the neo-cli
                Console.WriteLine($"Upgrade the neo-cli to {version}:");
                UpdateNeoCli(version);

                Console.WriteLine($"\nUpgrade the plugins to {version}:");
                foreach (var plugin in assets)
                {
                    var pluginName = Path.GetFileNameWithoutExtension(plugin["name"].GetString());
                    if (dllFiles.Contains(pluginName))
                        UpgradePlugin(pluginName, version);
                }
            }
            catch (WebException ex) when (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Error {ex.ToString()}");
                return;
            }
        }

        private static void UpdateNeoCli(string version)
        {
            string file = "neo-cli-win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                file = "neo-cli-osx-x64";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                file = "neo-cli-linux-x64";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                file = "neo-cli-win-x64";

            HttpWebRequest request = WebRequest.CreateHttp($"https://github.com/neo-project/neo-node/releases/download/{version}/{file}.zip");
            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex) when (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
            {
                response = DownloadFromAPI(version, "neo-node", file);
            }
            using (response)
            {
                using Stream stream = response.GetResponseStream();
                using ZipArchive zip = new(stream, ZipArchiveMode.Read);
                try
                {
                    zip.ExtractToDirectory(Temp, true);
                    CopyFilesRecursively($"{Temp}/neo-cli", ".");
                    Console.WriteLine($"{file}\t upgrade successfully.");
                }
                catch (IOException)
                {
                    Console.WriteLine("Error: Failed to upgrade the neo-cli, please close neo-cli first.");
                }
            }
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <param name="pluginName">the name of the plugin</param>
        /// <param name="version">the version of the latest neo</param>
        private static void UpgradePlugin(string pluginName, string version)
        {
            HttpWebRequest request = WebRequest.CreateHttp($"https://github.com/neo-project/neo-modules/releases/download/v{version}/{pluginName}.zip");
            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex) when (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
            {
                response = DownloadFromAPI(version, "neo-modules", pluginName);
            }
            using (response)
            {
                using Stream stream = response.GetResponseStream();
                using ZipArchive zip = new(stream, ZipArchiveMode.Read);
                try
                {
                    var temp = Path.Combine(Path.GetTempPath());
                    zip.ExtractToDirectory($"{temp}/{pluginName}", true);
                    CopyFilesRecursively($"{temp}/{pluginName}/", $"./");
                    Console.WriteLine($"{pluginName}\t upgrade successfully.");
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Error: Failed to upgrade {pluginName}, please close neo-cli first.");
                }
            }
        }

        /// <summary>
        /// Download the file from url of the api.github
        /// </summary>
        /// <param name="version">the latest neo version</param>
        /// <param name="repo">github repo of the target project</param>
        /// <param name="fileName">the name of the file to download</param>
        /// <returns></returns>
        private static HttpWebResponse DownloadFromAPI(string version, string repo, string fileName)
        {
            version = version.Substring(1);
            Version version_core = Version.Parse(version);
            var request = WebRequest.CreateHttp($"https://api.github.com/repos/neo-project/{repo}/releases");
            request.UserAgent = "Foo";
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
                .FirstOrDefault(p => p["name"].GetString() == $"{fileName}.zip");
            if (asset is null) throw new Exception("Plugin doesn't exist.");
            request = WebRequest.CreateHttp(asset["browser_download_url"].GetString());
            return (HttpWebResponse)request.GetResponse();
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                var path = dirPath.Replace(sourcePath, targetPath);
                if (Directory.Exists(path)) continue;
                Directory.CreateDirectory(path);
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    var targetFile = newPath.Replace(sourcePath, targetPath);
                    // Avoid config.json and other .json files
                    if (Path.GetExtension(newPath) != ".json" || !File.Exists(targetFile))
                        File.Copy(newPath, targetFile, true);
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}
