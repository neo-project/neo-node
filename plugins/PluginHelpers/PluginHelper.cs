// Copyright (C) 2015-2026 The Neo Project.
//
// PluginHelper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable
using System;
using System.IO;
using System.Text.Json;
using static System.IO.Path;

namespace Neo.Plugins;

/// <summary>
/// Helper class for managing unified storage paths across plugins.
/// Allows plugins to use a common base path configured in config.json.
/// This is a shared source file included by plugins that need unified storage path functionality.
/// </summary>
public static class PluginHelper
{
    /// <summary>
    /// Finds a file by searching upward from the specified directory.
    /// Returns the full path if found, null otherwise.
    /// </summary>
    private static string? FindFile(string fileName, string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);
        while (currentDir != null)
        {
            var filePath = Combine(currentDir.FullName, fileName);
            if (File.Exists(filePath))
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
    private static string? TryGetUnifiedStorageBasePath()
    {
        try
        {
            var configFile = FindFile("config.json", Environment.CurrentDirectory);
            if (configFile is null) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(configFile));
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
    /// <param name="pluginPath">The plugin-specific storage path.</param>
    /// <returns>The combined path if base path is configured, otherwise the original pluginPath.</returns>
    public static string ApplyUnifiedStoragePath(string pluginPath)
    {
        var basePath = TryGetUnifiedStorageBasePath();
        return basePath is null ? pluginPath : Combine(basePath, pluginPath);
    }
}
