// Copyright (C) 2015-2026 The Neo Project.
//
// CommandLineApp.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.CommandLine;

namespace Neo.ConsoleUI;

internal sealed class CommandLineApp
{
    public RootCommand Root { get; }
    public Option<string?> Config { get; }
    public Option<string?> Wallet { get; }
    public Option<string?> Password { get; }
    public Option<string?> DbEngine { get; }
    public Option<string?> DbPath { get; }
    public Option<string[]> Plugins { get; }
    public Option<bool> NoVerify { get; }

    public CommandLineApp(IReadOnlyList<CommandInfo> commands, Func<string, bool> invoke)
    {
        Root = new RootCommand("NEO cross-platform console UI (menus, hotkeys, line editing). Same commands as neo-cli.");
        Config = NewOption<string?>("--config", "Specifies the config file.", "-c", "/config");
        Wallet = NewOption<string?>("--wallet", "The path of the neo3 wallet [*.json].", "-w", "/wallet");
        Password = NewOption<string?>("--password", "Password to decrypt the wallet.", "-p", "/password");
        DbEngine = NewOption<string?>("--db-engine", "Specify the db engine.", "/db-engine");
        DbPath = NewOption<string?>("--db-path", "Specify the db path.", "/db-path");
        Plugins = new Option<string[]>("--plugins")
        {
            Description = "Plugins to install if not already present [plugin1 plugin2].",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        Plugins.Aliases.Add("/plugins");
        NoVerify = NewOption<bool>("--noverify", "Skip block verification when importing.", "/noverify");

        foreach (var option in new Option[] { Config, Wallet, Password, DbEngine, DbPath, Plugins, NoVerify })
            Root.Options.Add(option);

        // Root must have an action so `neo-tui` with only options (or none) is valid.
        Root.SetAction(static _ => { });

        foreach (var info in commands)
            AddCommand(Root, info, invoke);
    }

    public string[] ToMainServiceArgs(ParseResult result)
    {
        var args = new List<string>();
        Add(result, Config, args);
        Add(result, Wallet, args);
        Add(result, Password, args);
        Add(result, DbEngine, args);
        Add(result, DbPath, args);
        var plugins = result.GetValue(Plugins);
        if (plugins is { Length: > 0 })
        {
            args.Add("--plugins");
            args.AddRange(plugins);
        }
        if (result.GetValue(NoVerify))
            args.Add("--noverify");
        return [.. args];
    }

    private static void Add(ParseResult result, Option<string?> option, List<string> args)
    {
        var value = result.GetValue(option);
        if (string.IsNullOrEmpty(value))
            return;
        args.Add(option.Name);
        args.Add(value);
    }

    private static void AddCommand(Command parent, CommandInfo info, Func<string, bool> invoke)
    {
        var current = parent;
        foreach (var verb in info.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var existing = current.Subcommands.FirstOrDefault(c => c.Name == verb);
            if (existing is null)
            {
                existing = new Command(verb);
                current.Subcommands.Add(existing);
            }
            current = existing;
        }

        current.Description = string.IsNullOrWhiteSpace(info.Description) ? info.Key : info.Description;

        if (current.Arguments.All(a => a.Name != "args"))
        {
            var rest = new Argument<string[]>("args")
            {
                Description = "Command arguments (same syntax as neo-cli).",
                Arity = ArgumentArity.ZeroOrMore
            };
            current.Arguments.Add(rest);
            current.SetAction(parseResult =>
            {
                var extra = parseResult.GetValue(rest) ?? [];
                var line = extra.Length == 0 ? info.Key : $"{info.Key} {string.Join(' ', extra)}";
                invoke(line);
            });
        }
    }

    private static Option<T> NewOption<T>(string name, string description, params string[] aliases)
    {
        var option = new Option<T>(name) { Description = description };
        foreach (var alias in aliases)
            option.Aliases.Add(alias);
        return option;
    }
}
