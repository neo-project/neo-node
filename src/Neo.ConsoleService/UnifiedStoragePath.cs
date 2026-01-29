// Copyright (C) 2015-2026 The Neo Project.
//
// UnifiedStoragePath.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo;

/// <summary>
/// Helpers to read unified storage base path from config.json (Neo.CLI)
/// and combine plugin-specific storage paths under that base directory.
/// </summary>
public static class UnifiedStoragePath
{
    /// <summary>
    /// Finds a file by searching upward from the specified directory.
    /// Returns the full path if found, null otherwise.
    /// </summary>
    private static string? FindFile(string fileName, string startDirectory)
    {
        var currentDir = new System.IO.DirectoryInfo(startDirectory);
        while (currentDir != null)
        {
            var filePath = System.IO.Path.Combine(currentDir.FullName, fileName);
            if (System.IO.File.Exists(filePath))
            {
                return filePath;
            }
            currentDir = currentDir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Gets unified storage base path from config.json when configured as a base directory.
    /// Base path is defined as a non-empty Path WITHOUT "{0}" placeholder.
    /// Returns null if config.json is not found, invalid, or not configured as base path.
    /// </summary>
    public static string? TryGetBasePath()
    {
        try
        {
            var configFile = FindFile("config.json", Environment.CurrentDirectory);
            if (configFile is null) return null;

            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(configFile));
            if (!doc.RootElement.TryGetProperty("ApplicationConfiguration", out var appCfg)) return null;
            if (!appCfg.TryGetProperty("Storage", out var storageCfg)) return null;
            if (!storageCfg.TryGetProperty("Path", out var pathEl)) return null;

            var path = pathEl.GetString();
            return string.IsNullOrEmpty(path) || path.Contains("{0}") ? null : path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Combines plugin path under unified base path (if configured).
    /// If base path is not configured, returns pluginPath as-is.
    /// </summary>
    public static string Apply(string pluginPath)
    {
        var basePath = TryGetBasePath();
        return basePath is null ? pluginPath : System.IO.Path.Combine(basePath, pluginPath);
    }
}

