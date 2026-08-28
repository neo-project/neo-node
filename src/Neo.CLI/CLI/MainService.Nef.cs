// Copyright (C) 2015-2026 The Neo Project.
//
// MainService.Nef.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Extensions;
using Neo.IO;
using Neo.SmartContract;

namespace Neo.CLI;

partial class MainService
{
    /// <summary>
    /// Convert a <c>.nef</c> file to C# pseudocode and write it to disk.
    /// </summary>
    /// <param name="nefPath">Path to the NEF file.</param>
    /// <param name="outputPath">Destination <c>.cs</c> path. Defaults to the NEF path with a <c>.cs</c> extension.</param>
    [ConsoleCommand("export csharp", Category = "Contract Commands", Description = "Convert a .nef file to C# pseudocode and write it to disk.")]
    private void OnExportCSharpCommand(string nefPath, string? outputPath = null)
    {
        if (!File.Exists(nefPath))
        {
            ConsoleHelper.Error($"NEF file not found: {nefPath}");
            return;
        }

        NefFile nef;
        try
        {
            var data = File.ReadAllBytes(nefPath);
            try
            {
                nef = data.AsSerializable<NefFile>();
            }
            catch (FormatException)
            {
                nef = NefFile.Parse(data, verify: false);
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"Failed to parse NEF: {GetExceptionMessage(ex)}");
            return;
        }

        outputPath ??= Path.ChangeExtension(nefPath, ".cs");
        if (Directory.Exists(outputPath))
            outputPath = Path.Combine(outputPath, Path.ChangeExtension(Path.GetFileName(nefPath), ".cs"));

        try
        {
            var className = NefCSharpWriter.ToClassName(nefPath);
            var csharp = NefCSharpWriter.Generate(nef, className);
            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, csharp);
            ConsoleHelper.Info("Wrote C# pseudocode: ", Path.GetFullPath(outputPath));
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"Failed to write C#: {GetExceptionMessage(ex)}");
        }
    }
}
