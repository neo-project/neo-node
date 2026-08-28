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

namespace Neo.ConsoleUI;

static class Program
{
    static int Main(string[] args)
    {
        var service = new MainService();
        var commands = CommandCatalog.Load(service);
        bool Invoke(string line)
            => service.OnCommand(line);

        var app = new CommandLineApp(commands, Invoke);
        var parseResult = app.Root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(error.Message)}[/]");
            return 1;
        }

        if (!service.OnStart(app.ToMainServiceArgs(parseResult)))
            return 1;

        try
        {
            if (parseResult.CommandResult.Command != app.Root)
            {
                parseResult.Invoke();
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
}
