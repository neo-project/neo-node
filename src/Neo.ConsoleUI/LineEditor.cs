// Copyright (C) 2015-2026 The Neo Project.
//
// LineEditor.cs file belongs to the neo project and is free
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
/// Cross-platform line editor: arrows, Home/End, history, Tab complete, Ctrl+U/A/E.
/// </summary>
internal sealed class LineEditor
{
    private readonly List<string> _history = [];
    private readonly IReadOnlyList<string> _completions;
    private const int HistorySize = 200;

    public LineEditor(IReadOnlyList<string> completions)
    {
        _completions = completions;
    }

    public string? Read(string prompt)
    {
        var input = new System.Text.StringBuilder();
        var cursor = 0;
        var historyIndex = -1;

        void Redraw()
        {
            AnsiConsole.Cursor.Show();
            var line = prompt + input;
            AnsiConsole.Write("\r" + line + new string(' ', Math.Max(0, Console.WindowWidth - line.Length - 1)));
            var left = Math.Min(prompt.Length + cursor, Math.Max(0, Console.WindowWidth - 1));
            try
            {
                Console.SetCursorPosition(left, Console.CursorTop);
            }
            catch
            {
                // Some redirected consoles reject SetCursorPosition.
            }
        }

        Redraw();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                AnsiConsole.WriteLine();
                var result = input.ToString();
                if (!string.IsNullOrWhiteSpace(result))
                {
                    _history.Add(result);
                    if (_history.Count > HistorySize)
                        _history.RemoveAt(0);
                }
                return result;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                AnsiConsole.WriteLine();
                return null;
            }

            if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                return null;

            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (cursor > 0) cursor--;
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursor < input.Length) cursor++;
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.Home || (key.Key == ConsoleKey.A && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
            {
                cursor = 0;
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.End || (key.Key == ConsoleKey.E && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
            {
                cursor = input.Length;
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.U && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                input.Clear();
                cursor = 0;
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    input.Remove(cursor - 1, 1);
                    cursor--;
                }
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.Delete)
            {
                if (cursor < input.Length)
                    input.Remove(cursor, 1);
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                if (_history.Count == 0) continue;
                historyIndex = historyIndex < 0 ? _history.Count - 1 : Math.Max(0, historyIndex - 1);
                input.Clear();
                input.Append(_history[historyIndex]);
                cursor = input.Length;
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (historyIndex < 0) continue;
                historyIndex++;
                input.Clear();
                if (historyIndex >= _history.Count)
                {
                    historyIndex = -1;
                }
                else
                {
                    input.Append(_history[historyIndex]);
                }
                cursor = input.Length;
                Redraw();
                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                var prefix = input.ToString();
                var match = _completions.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    input.Clear();
                    input.Append(match);
                    cursor = input.Length;
                    Redraw();
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                input.Insert(cursor, key.KeyChar);
                cursor++;
                Redraw();
            }
        }
    }
}
