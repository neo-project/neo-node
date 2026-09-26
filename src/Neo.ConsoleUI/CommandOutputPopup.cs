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
using System.Text;

namespace Neo.ConsoleUI;

/// <summary>
/// Captures neo-cli command output and shows it in a panel on this console.
/// </summary>
internal static class CommandOutputPopup
{
    public static bool Run(string commandLine, Func<string, bool> invoke, string? displayLine = null)
    {
        var found = true;
        var display = displayLine ?? commandLine;
        while (true)
        {
            var result = Execute(commandLine, display, invoke);
            found = result.Found;
            if (!Show(display, result.Output, result.Ok))
                return found;
        }
    }

    private static (bool Found, bool Ok, string Output) Execute(string commandLine, string display, Func<string, bool> invoke)
    {
        var buffer = new StringWriter();
        var stdout = Console.Out;
        var stderr = Console.Error;
        var found = true;
        var ok = true;

        AnsiConsole.Clear();
        // Do not wrap invoke in Status(): wallet/relay prompts use Console.ReadKey
        // and must stay visible. Tee so prompts appear while output is captured.
        AnsiConsole.MarkupLine($"[grey]Please wait — running {Markup.Escape(display)}[/]");
        var tee = new TeeTextWriter(buffer, stdout);
        try
        {
            Console.SetOut(tee);
            Console.SetError(tee);
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

        return (found, ok && found, buffer.ToString());
    }

    /// <returns><see langword="true"/> to rerun the command.</returns>
    private static bool Show(string display, string output, bool ok)
    {
        AnsiConsole.Clear();
        var width = Math.Max(40, Console.WindowWidth - 2);
        var innerWidth = Math.Max(20, width - 4);
        var maxLines = Math.Max(6, Console.WindowHeight - 8);
        var body = string.IsNullOrWhiteSpace(output) ? "(no output)" : output.TrimEnd();
        body = WrapOutput(body, innerWidth, maxLines);

        var title = display.Length > 60
            ? string.Concat(display.AsSpan(0, 57), "…")
            : display;
        var panel = new Panel(new Text(body))
            .Header($" neo> {Markup.Escape(title)} ")
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

    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _capture;
        private readonly TextWriter _live;

        public TeeTextWriter(TextWriter capture, TextWriter live)
        {
            _capture = capture;
            _live = live;
        }

        public override Encoding Encoding => _capture.Encoding;

        public override void Write(char value)
        {
            _capture.Write(value);
            _live.Write(value);
        }

        public override void Write(string? value)
        {
            _capture.Write(value);
            _live.Write(value);
        }

        public override void WriteLine(string? value)
        {
            _capture.WriteLine(value);
            _live.WriteLine(value);
        }

        public override void Flush()
        {
            _capture.Flush();
            _live.Flush();
        }
    }
}
