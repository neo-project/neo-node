// Copyright (C) 2016-2022 The Neo Project.
//
// The update is free software distributed under the MIT software
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Neo.ConsoleService;

namespace update;

class Update
{
    private static string Temp => Path.GetTempPath();

    static async Task Main()
    {
        const string pluginPath = "Plugins";
        if (!Directory.Exists(pluginPath)) return;
        var pluginEntries = Directory.GetFiles(pluginPath, "*.dll").Select(Path.GetFileNameWithoutExtension).ToList();
        await GetLatestVersion(pluginEntries);
    }


    /// <summary>
    /// Download the most latest file
    /// </summary>
    /// <param name="dllFiles">The dll file in the local `Plugins` folder</param>
    private static async Task GetLatestVersion(List<string> dllFiles)
    {
        using HttpClient http = new();
        HttpRequestMessage request = new(HttpMethod.Get, $"https://api.github.com/repos/neo-project/neo-modules/releases/latest");
        request.Headers.UserAgent.ParseAdd("Update");
        var response = await http.SendAsync(request);
        try
        {
            using var stream = new StreamReader(await response.Content.ReadAsStreamAsync());
            var json = await stream.ReadToEndAsync();
            var objects = JObject.Parse(json); // parse as array
            var version = objects["name"]?.ToString();
            var assets = objects["assets"]?.ToArray();

            // Update the neo-cli
            ConsoleHelper.Info($"Upgrade the neo-cli to {version}:");
            await UpdateNeoCli(version);

            ConsoleHelper.Info($"\nUpgrade the plugins to {version}:");
            foreach (var plugin in assets!)
            {
                var pluginName = Path.GetFileNameWithoutExtension(plugin["name"]?.ToString());
                if (dllFiles.Contains(pluginName))
                    await UpgradePlugin(pluginName, version);
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error(ex.ToString());
        }
    }

    private static async Task UpdateNeoCli(string version)
    {
        string file = "neo-cli-win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            file = "neo-cli-osx-x64";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            file = "neo-cli-linux-x64";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            file = "neo-cli-win-x64";

        using HttpClient http = new();
        HttpResponseMessage response = await http.GetAsync($"https://github.com/neo-project/neo-node/releases/download/{version}/{file}.zip");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response = await DownloadFromApi(version, "neo-node", file);
        }
        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using ZipArchive zip = new(stream, ZipArchiveMode.Read);
            try
            {
                zip.ExtractToDirectory(Temp, true);
                CopyFilesRecursively($"{Temp}/neo-cli", ".");
                ConsoleHelper.Info($"{file}", "\t upgrade successful.");
            }
            catch (IOException)
            {
                ConsoleHelper.Error("Failed to upgrade the neo-cli, please close neo-cli first.");
            }
        }
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <param name="pluginName">the name of the plugin</param>
    /// <param name="version">the version of the latest neo</param>
    private static async Task UpgradePlugin(string pluginName, string version)
    {
        using HttpClient http = new();
        HttpResponseMessage response = await http.GetAsync($"https://github.com/neo-project/neo-modules/releases/download/v{version}/{pluginName}.zip");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response = await DownloadFromApi(version, "neo-modules", pluginName);
        }
        using (response)
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            using ZipArchive zip = new(stream, ZipArchiveMode.Read);
            try
            {
                var temp = Path.GetTempPath();
                zip.ExtractToDirectory($"{temp}/{pluginName}", true);
                CopyFilesRecursively($"{temp}/{pluginName}/", $"./");
                ConsoleHelper.Info($"{pluginName}", "\t upgrade successful.");
            }
            catch
            {
                ConsoleHelper.Error($"Failed to upgrade {pluginName}, please close neo-cli first.");
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
    private static async Task<HttpResponseMessage> DownloadFromApi(string version, string repo, string fileName)
    {
        using HttpClient http = new();
        version = version.TrimStart('v');
        Version versionCore = Version.Parse(version);
        HttpRequestMessage request = new(HttpMethod.Get, $"https://api.github.com/repos/neo-project/{repo}/releases");
        request.Headers.UserAgent.ParseAdd("Update");
        using HttpResponseMessage responseApi = await http.SendAsync(request);
        var buffer = await responseApi.Content.ReadAsStringAsync();
        var releases = JArray.Parse(buffer);
        var asset = releases
            .Where(p => !p["tag_name"].ToString().Contains('-'))
            .Select(p => new
            {
                Version = Version.Parse(p["tag_name"]?.ToString().TrimStart('v') ?? throw new ArgumentNullException()),
                Assets = JArray.Parse(p["assets"]?.ToString() ?? throw new ArgumentNullException())
            })
            .OrderByDescending(p => p.Version)
            .First(p => p.Version <= versionCore).Assets
            .FirstOrDefault(p => p["name"].ToString() == $"{fileName}.zip");
        if (asset is null) throw new Exception("Plugin doesn't exist.");
        return await http.GetAsync(asset["browser_download_url"]?.ToString());
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
        foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
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
                // ignored
            }
        }
    }
}
