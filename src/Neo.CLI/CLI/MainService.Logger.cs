// Copyright (C) 2015-2026 The Neo Project.
//
// MainService.Logger.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Neo.CLI;

partial class MainService
{
    private readonly object syncRoot = new();

    private string _logPath = "";

    private readonly LoggingLevelSwitch _logLevel = new();

    private readonly LoggingLevelSwitch _consoleLevel = new();

    /// <summary>
    /// Initialize the logger. it should be called when system is starting.
    /// </summary>
    private void SetupLogger(string logPath, LogLevel level, bool showConsoleLog)
    {
        _logLevel.MinimumLevel = (LogEventLevel)level;
        _logPath = logPath;
        _consoleLevel.MinimumLevel = showConsoleLog ? _logLevel.MinimumLevel : LogEventLevel.Fatal;
        Logs.LoggerFactory = CreateLogger;
    }

    private ILogger CreateLogger(string source)
    {
        if (string.IsNullOrEmpty(_logPath)) return new LoggerConfiguration().CreateLogger();

        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_logLevel)
            .WriteTo.File(
                path: Path.Combine(_logPath, source, "log-.txt"),
                fileSizeLimitBytes: 100 * 1024 * 1024, // 100 MiB
                rollOnFileSizeLimit: true,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30 // about 1 month
            )
            .WriteTo.Console(levelSwitch: _consoleLevel, syncRoot: syncRoot)
            .CreateLogger();
    }

    /// <summary>
    /// Process "console log off" command to turn off console log
    /// </summary>
    [ConsoleCommand("console log off", Category = "Log Commands")]
    private void OnLogOffCommand()
    {
        _consoleLevel.MinimumLevel = LogEventLevel.Fatal;
    }

    /// <summary>
    /// Process "console log on" command to turn on the console log
    /// </summary>
    [ConsoleCommand("console log on", Category = "Log Commands")]
    private void OnLogOnCommand()
    {
        _consoleLevel.MinimumLevel = _logLevel.MinimumLevel;
    }
}
