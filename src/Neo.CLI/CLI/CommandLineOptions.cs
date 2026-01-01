// Copyright (C) 2015-2026 The Neo Project.
//
// CommandLineOptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.CLI;

public class CommandLineOptions
{
    [Option("--config", "-c", "/config", Description = "Specifies the config file.")]
    public string? Config { get; init; }
    [Option("--wallet", "-w", "/wallet", Description = "The path of the neo3 wallet [*.json].")]
    public string? Wallet { get; init; }
    [Option("--password", "-p", "/password", Description = "Password to decrypt the wallet, either from the command line or config file.")]
    public string? Password { get; init; }
    [Option("--plugins", "/plugins", Description = "The list of plugins, if not present, will be installed [plugin1 plugin2].")]
    public string[]? Plugins { get; set; }
    [Option("--db-engine", "/db-engine", Description = "Specify the db engine.")]
    public string? DBEngine { get; init; }
    [Option("--db-path", "/db-path", Description = "Specify the db path.")]
    public string? DBPath { get; init; }
    [Option("--verbose", "/verbose", Description = "The verbose log level, if not present, will be info.")]
    public LogLevel Verbose { get; init; } = LogLevel.Info;
    [Option("--noverify", "/noverify", Description = "Indicates whether the blocks need to be verified when importing.")]
    public bool? NoVerify { get; init; }
    [Option("--background", "/background", Description = "Run the service in background.")]
    public bool Background { get; init; }

    /// <summary>
    /// Check if CommandLineOptions was configured
    /// </summary>
    public bool IsValid =>
            !string.IsNullOrEmpty(Config) ||
            !string.IsNullOrEmpty(Wallet) ||
            !string.IsNullOrEmpty(Password) ||
            !string.IsNullOrEmpty(DBEngine) ||
            !string.IsNullOrEmpty(DBPath) ||
            (Plugins?.Length > 0) ||
            NoVerify is not null;
}
