// Copyright (C) 2016-2021 The Neo Project.
// 
// The neo-cli is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.IO.Path;

namespace Neo.CLI;

internal class Logger : Plugin, ILogPlugin
{
    private static readonly ConsoleColorSet DebugColor = new ConsoleColorSet(ConsoleColor.Cyan);
    private static readonly ConsoleColorSet InfoColor = new ConsoleColorSet(ConsoleColor.White);
    private static readonly ConsoleColorSet WarningColor = new ConsoleColorSet(ConsoleColor.Yellow);
    private static readonly ConsoleColorSet ErrorColor = new ConsoleColorSet(ConsoleColor.Red);
    private static readonly ConsoleColorSet FatalColor = new ConsoleColorSet(ConsoleColor.Red);
    private static readonly ConsoleColorSet KeyColor = new ConsoleColorSet(ConsoleColor.DarkGreen);
    public override string Name => "SystemLog";
    public override string Description => "Prints consensus log and is a built-in plugin which cannot be uninstalled";
    public override string ConfigFile => Combine(GetDirectoryName(Path), "config.json");
    public override string Path => GetType().Assembly.Location;
    private bool showLog = Settings.Default.Logger.ConsoleOutput;

    /// <summary>
    /// Process "log off" command
    /// </summary>
    [ConsoleCommand("log off", Category = "Log Commands")]
    private void OnLogOffCommand()
    {
        showLog = false;
    }

    /// <summary>
    /// Process "log on" command
    /// </summary>
    [ConsoleCommand("log on", Category = "Log Commands")]
    private void OnLogOnCommand()
    {
        if (Settings.Default.Logger.ConsoleOutput) showLog = true;
    }


    private static void GetErrorLogs(StringBuilder sb, Exception ex)
    {
        sb.AppendLine(ex.GetType().ToString());
        sb.AppendLine(ex.Message);
        sb.AppendLine(ex.StackTrace);
        if (ex is AggregateException ex2)
        {
            foreach (Exception inner in ex2.InnerExceptions)
            {
                sb.AppendLine();
                GetErrorLogs(sb, inner);
            }
        }
        else if (ex.InnerException != null)
        {
            sb.AppendLine();
            GetErrorLogs(sb, ex.InnerException);
        }
    }

    void ILogPlugin.Log(string source, LogLevel level, object message)
    {
        if (!Settings.Default.Logger.Active)
            return;

        if (message is Exception ex)
        {
            var sb = new StringBuilder();
            GetErrorLogs(sb, ex);
            message = sb.ToString();
        }

        lock (typeof(Logger))
        {
            DateTime now = DateTime.Now;
            var log = $"[{now.TimeOfDay:hh\\:mm\\:ss\\.fff}]";
            if (Settings.Default.Logger.ConsoleOutput && showLog)
            {
                var currentColor = InfoColor;
                var messages = message is not string msg ? new[] { $"{ message}" } : Parse(msg);
                ConsoleColorSet logcolor;
                string loglevel;
                switch (level)
                {
                    case LogLevel.Debug:
                        {
#if RELEASE
                            return;
#endif
                            logcolor = DebugColor;
                            loglevel = "DEBUG";
                            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                                messages[0] = $"🔨{messages[0]}";
                            break;
                        }

                    case LogLevel.Error:
                        logcolor = ErrorColor; loglevel = "ERROR";
                        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                            messages[0] = $"❌{messages[0]}";
                        break;
                    case LogLevel.Fatal: logcolor = FatalColor; loglevel = "FATAL"; break;
                    case LogLevel.Info: logcolor = KeyColor; loglevel = "INFO"; break;
                    case LogLevel.Warning:
                        logcolor = WarningColor; loglevel = "WARN";
                        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                            messages[0] = $"⚠{messages[0]}";
                        break;
                    default: logcolor = InfoColor; loglevel = "INFO"; break;
                }

                Console.OutputEncoding = Encoding.Unicode;

                logcolor.Apply();
                Console.Write(loglevel + " ");
                currentColor.Apply();
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    if (messages[0].Contains("Sending")) messages[0] = $"✈{messages[0]}";
                    if (messages[0].Contains("Received")) messages[0] = $"✉{messages[0]}";
                    if (messages[0].Contains("Persisted")) messages[0] = $"📦{messages[0]}";
                }
                Console.Write($"{log} {messages[0]}");
                for (var i = 0; i < 35 - messages[0].Length - loglevel.Length; i++) Console.Write(' ');
                for (var i = 1; i < messages.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        currentColor.Apply();
                        Console.Write($"={messages[i]} ");
                    }
                    else
                    {
                        logcolor.Apply();
                        Console.Write($" {messages[i]}");
                    }
                }
                currentColor.Apply();
                Console.WriteLine();
            }

            if (string.IsNullOrEmpty(Settings.Default.Logger.Path)) return;
            var sb = new StringBuilder(source);
            foreach (var c in GetInvalidFileNameChars())
                sb.Replace(c, '-');
            var path = Combine(Settings.Default.Logger.Path, sb.ToString());
            Directory.CreateDirectory(path);
            path = Combine(path, $"{now:yyyy-MM-dd}.log");
            try
            {
                File.AppendAllLines(path, new[] { $"[{level}]{log}" });
            }
            catch (IOException)
            {
                Console.WriteLine("Error writing the log file: " + path);
            }
        }
    }

    private static string[] Parse(string message)
    {
        var equals = message.Trim().Split('=');

        if (equals.Length == 1) return new[] { message };

        var messages = new List<string>();
        foreach (var t in @equals)
        {
            var msg = t.Trim();
            var parts = msg.Split(' ');
            var d = parts.Take(parts.Length - 1);

            if (parts.Length > 1)
            {
                messages.Add(string.Join(" ", d));
            }
            messages.Add(parts.LastOrDefault());
        }

        return messages.ToArray();
    }

}
