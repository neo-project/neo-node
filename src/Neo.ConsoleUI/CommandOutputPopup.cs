// Copyright (C) 2015-2026 The Neo Project.
//
// CommandOutputPopup.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Spectre.Console;

namespace Neo.ConsoleUI;

/// <summary>
/// Captures neo-cli command output and shows it in a panel on this console.
/// </summary>
internal static class CommandOutputPopup
{
    public static bool Run(string commandLine, Func<string, bool> invoke)
    {
        var found = true;
        while (true)
        {
            var result = Execute(commandLine, invoke);
            found = result.Found;
            if (!Show(commandLine, result.Output, result.Ok))
                return found;
        }
    }

    private static (bool Found, bool Ok, string Output) Execute(string commandLine, Func<string, bool> invoke)
    {
        var buffer = new StringWriter();
        var stdout = Console.Out;
        var stderr = Console.Error;
        var found = true;
        var ok = true;

        AnsiConsole.Clear();
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .Start($"Please wait — running {commandLine}", _ =>
            {
                try
                {
                    Console.SetOut(buffer);
                    Console.SetError(buffer);
                    found = invoke(commandLine);
                    if (!found)
                        buffer.WriteLine("Command not found");
                }
                catch (Exception ex)
                {
                    ok = false;
                    buffer.WriteLine(ex.InnerException?.Message ?? ex.Message);
                }
                finally
                {
                    Console.SetOut(stdout);
                    Console.SetError(stderr);
                }
            });

        return (found, ok && found, buffer.ToString());
    }

    /// <returns><see langword="true"/> to rerun the command.</returns>
    private static bool Show(string commandLine, string output, bool ok)
    {
        AnsiConsole.Clear();
        var width = Math.Max(40, Console.WindowWidth - 2);
        var innerWidth = Math.Max(20, width - 4);
        var maxLines = Math.Max(6, Console.WindowHeight - 8);
        var body = string.IsNullOrWhiteSpace(output) ? "(no output)" : output.TrimEnd();
        body = WrapOutput(body, innerWidth, maxLines);

        var title = commandLine.Length > 60
            ? string.Concat(commandLine.AsSpan(0, 57), "…")
            : commandLine;
        var panel = new Panel(new Text(body))
            .Header($" neo> {title} ")
            .Border(BoxBorder.Rounded)
            .BorderColor(ok ? Color.Green : Color.Red)
            .Padding(1, 0, 1, 0)
            .Expand();
        AnsiConsole.Write(panel);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[grey]Command finished[/]")
                .AddChoices("Rerun command", "Close"));
        return choice == "Rerun command";
    }

    private static string WrapOutput(string output, int width, int maxLines)
    {
        output = output
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\t', ' ');
        var wrapped = new List<string>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw;
            if (line.Length == 0)
            {
                wrapped.Add(string.Empty);
                continue;
            }

            while (line.Length > width)
            {
                wrapped.Add(line[..width]);
                line = line[width..];
            }

            wrapped.Add(line);
        }

        if (wrapped.Count <= maxLines)
            return string.Join('\n', wrapped);
        return string.Join('\n', wrapped.Take(maxLines - 1))
               + $"\n… ({wrapped.Count - maxLines + 1} more lines)";
    }
}
