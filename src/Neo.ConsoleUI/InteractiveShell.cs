// Copyright (C) 2015-2026 The Neo Project.
//
// InteractiveShell.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.CLI;
using Spectre.Console;
using System.Reflection;

namespace Neo.ConsoleUI;

internal sealed class InteractiveShell
{
    private readonly MainService _service;
    private readonly IReadOnlyList<CommandInfo> _commands;
    private readonly LineEditor _editor;
    private readonly Func<string, bool> _invoke;

    public InteractiveShell(MainService service, IReadOnlyList<CommandInfo> commands, Func<string, bool> invoke)
    {
        _service = service;
        _commands = commands;
        _invoke = invoke;
        _editor = new LineEditor(CommandCatalog.Completions(commands));
    }

    public void Run()
    {
        using var broadcastCancel = new CancellationTokenSource();
        Task broadcast = Task.CompletedTask;
        try
        {
            if (_service.NeoSystem is not null)
                broadcast = _service.CreateBroadcastTask(broadcastCancel.Token);
        }
        catch
        {
            // Node failed to start; still show the status screen.
        }

        try
        {
            while (true)
            {
                var next = StateScreen.Run(_service);
                if (next == StatusAction.Quit)
                    break;
                if (next == StatusAction.Help)
                {
                    AnsiConsole.Clear();
                    PrintHotkeys();
                    AnsiConsole.MarkupLine("[grey]Press any key to return to status[/]");
                    Console.ReadKey(intercept: true);
                    continue;
                }
                if (next == StatusAction.CommandLine)
                {
                    AnsiConsole.Clear();
                    RunCommandLine();
                    continue;
                }

                AnsiConsole.Clear();
                if (!RunMenu())
                    break;
            }
        }
        finally
        {
            broadcastCancel.Cancel();
            try { broadcast.Wait(TimeSpan.FromSeconds(2)); } catch { /* cancelled */ }
        }
    }

    /// <returns><see langword="false"/> when the user chose Exit.</returns>
    private bool RunMenu()
    {
        WriteStatus();
        var menu = new[] { "Back to status", "Command line", "Command palette" }
            .Concat(CommandCatalog.Categories(_commands))
            .Concat(["Help", "Exit"]);
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select an action[/] (search with typing)")
                .PageSize(15)
                .EnableSearch()
                .AddChoices(menu));

        if (action == "Exit")
            return false;
        if (action == "Back to status")
            return true;
        if (action == "Help")
        {
            PrintHotkeys();
            RunLine("help");
            return true;
        }
        if (action == "Command line")
        {
            RunCommandLine();
            return true;
        }
        if (action == "Command palette")
        {
            RunPalette();
            return true;
        }

        RunCategory(action);
        return true;
    }

    private void RunCategory(string category)
    {
        var items = _commands.Where(c => c.Category == category).ToArray();
        if (items.Length == 0)
            return;

        var labels = items.Select(FormatChoice).Concat([".. Back"]).ToArray();
        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[yellow]{category}[/]")
                .PageSize(20)
                .EnableSearch()
                .AddChoices(labels));
        if (picked == ".. Back")
            return;

        var index = Array.IndexOf(labels, picked);
        if (index >= 0 && index < items.Length)
        {
            if (items[index].Key.Equals("show state", StringComparison.OrdinalIgnoreCase))
                return;
            RunCommand(items[index]);
        }
    }

    private void RunPalette()
    {
        var labels = _commands.Select(FormatChoice).ToArray();
        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]All commands[/] (type to search)")
                .PageSize(20)
                .EnableSearch()
                .AddChoices(labels));
        var index = Array.IndexOf(labels, picked);
        if (index >= 0)
        {
            if (_commands[index].Key.Equals("show state", StringComparison.OrdinalIgnoreCase))
                return;
            RunCommand(_commands[index]);
        }
    }

    private void RunCommandLine()
    {
        AnsiConsole.MarkupLine("[grey]Tab complete · arrows move · Up/Down history · Esc back · Ctrl+C cancel[/]");
        while (true)
        {
            var line = _editor.Read("neo> ");
            if (line is null)
                return;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("quit", StringComparison.OrdinalIgnoreCase))
                return;
            if (line.Trim().Equals("show state", StringComparison.OrdinalIgnoreCase))
                return;
            RunLine(line.Trim());
        }
    }

    private void RunCommand(CommandInfo command)
    {
        var parameters = command.Method.GetParameters();
        var parts = new List<string> { command.Key };
        foreach (var parameter in parameters)
        {
            var hint = parameter.HasDefaultValue
                ? $"optional, default {parameter.DefaultValue ?? "null"}"
                : "required";
            var title = $"[green]{parameter.Name}[/] ({parameter.ParameterType.Name}, {hint})";
            if (IsPassword(parameter))
            {
                var secret = AnsiConsole.Prompt(new TextPrompt<string>(title).Secret());
                parts.Add(Quote(secret));
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                var value = AnsiConsole.Prompt(
                    new TextPrompt<string>(title + " [[empty = default]]")
                        .AllowEmpty());
                if (string.IsNullOrEmpty(value))
                    continue;
                parts.Add(Quote(value));
            }
            else
            {
                var value = AnsiConsole.Prompt(new TextPrompt<string>(title));
                parts.Add(Quote(value));
            }
        }

        var line = string.Join(' ', parts);
        RunLine(line);
    }

    private bool RunLine(string line)
        => CommandOutputPopup.Run(line, _invoke);

    private void WriteStatus()
    {
        var wallet = _service.CurrentWallet is null ? "closed" : _service.CurrentWallet.Name;
        string height;
        try
        {
            height = _service.NeoSystem is null ? "-" : "?";
            if (_service.NeoSystem is not null)
                height = Neo.SmartContract.Native.NativeContract.Ledger.CurrentIndex(_service.NeoSystem.StoreView).ToString();
        }
        catch
        {
            height = "-";
        }

        AnsiConsole.Write(new Rule($"[green]wallet[/] {Markup.Escape(wallet)}  [green]height[/] {height}").RuleStyle("grey"));
    }

    private static void PrintHotkeys()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Key");
        table.AddColumn("Action");
        table.AddRow("M / Enter (status)", "Open command menu");
        table.AddRow("C (status)", "Command line");
        table.AddRow("H (status)", "Help");
        table.AddRow("↑ ↓ (status)", "Scroll connected peers");
        table.AddRow("Q / Esc (status)", "Quit");
        table.AddRow("Type to search", "Filter the current menu");
        table.AddRow("Enter", "Select");
        table.AddRow("Esc (command line)", "Back to menu");
        table.AddRow("Tab (command line)", "Complete command");
        table.AddRow("↑ ↓ (command line)", "History");
        table.AddRow("Ctrl+A / Ctrl+E", "Line start / end");
        table.AddRow("Ctrl+U", "Clear line");
        table.AddRow("Ctrl+C", "Cancel line / later Exit");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static string FormatChoice(CommandInfo command)
        => string.IsNullOrWhiteSpace(command.Description)
            ? command.Key
            : $"{command.Key}  [grey]{Markup.Escape(command.Description)}[/]";

    private static bool IsPassword(ParameterInfo parameter)
        => parameter.Name is not null && parameter.Name.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static string Quote(string value)
        => value.Contains(' ', StringComparison.Ordinal) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
}
