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

namespace Neo.CLI;

partial class MainService
{
    private static readonly ConsoleColorSet DebugColor = new(ConsoleColor.Cyan);
    private static readonly ConsoleColorSet InfoColor = new(ConsoleColor.White);
    private static readonly ConsoleColorSet WarningColor = new(ConsoleColor.Yellow);
    private static readonly ConsoleColorSet ErrorColor = new(ConsoleColor.Red);
    private static readonly ConsoleColorSet FatalColor = new(ConsoleColor.Red);

    private readonly object syncRoot = new();
    private bool _showLog = Settings.Default.Logger.ConsoleOutput;

    /// <summary>
    /// Process "console log off" command to turn off console log
    /// </summary>
    [ConsoleCommand("console log off", Category = "Log Commands")]
    private void OnLogOffCommand()
    {
        _showLog = false;
    }

    /// <summary>
    /// Process "console log on" command to turn on the console log
    /// </summary>
    [ConsoleCommand("console log on", Category = "Log Commands")]
    private void OnLogOnCommand()
    {
        _showLog = true;
    }
}
