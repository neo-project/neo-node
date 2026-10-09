// Copyright (C) 2015-2026 The Neo Project.
//
// Program.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.CLI;
using Spectre.Console;
using System.CommandLine;

namespace Neo.ConsoleUI;

static class Program
{
    static int Main(string[] args)
    {
        // Match neo-cli: Plugins/, config.json, and auto-install zips are resolved from the exe directory.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var service = new MainService();
        var bootstrap = new CommandLineApp([], static _ => true);
        bootstrap.Root.TreatUnmatchedTokensAsErrors = false;
        var parseResult = bootstrap.Root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(error.Message)}[/]");
            return 1;
        }

        var startArgs = bootstrap.ToMainServiceArgs(parseResult);
        if (!startArgs.Contains("--config", StringComparer.OrdinalIgnoreCase))
        {
            var besideExe = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(besideExe))
                startArgs = [.. startArgs, "--config", besideExe];
        }

        if (!service.OnStart(startArgs))
            return 1;

        try
        {
            service.WhenStarted.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            service.OnStop();
            return 1;
        }

        bool Invoke(string line)
            => service.OnCommand(line);

        var commands = CommandCatalog.Load(service);
        var app = new CommandLineApp(commands, Invoke);
        var fullParse = app.Root.Parse(args);
        if (fullParse.Errors.Count > 0)
        {
            foreach (var error in fullParse.Errors)
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(error.Message)}[/]");
            service.OnStop();
            return 1;
        }

        try
        {
            if (fullParse.CommandResult.Command != app.Root && !IsShowState(fullParse))
            {
                fullParse.Invoke();
                return 0;
            }

            new InteractiveShell(service, commands, Invoke).Run();
            Invoke("exit");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            service.OnStop();
        }
    }

    private static bool IsShowState(ParseResult parse)
    {
        var command = parse.CommandResult.Command;
        return command.Name == "state" &&
               command.Parents.OfType<Command>().Any(c => c.Name == "show");
    }
}
