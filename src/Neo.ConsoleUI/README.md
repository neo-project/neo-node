# neo-tui

Cross-platform console UI for **neo-cli** (Windows, Linux, macOS).

It starts the same `MainService` node as `neo-cli`, then offers:

- Category **menus** (arrow keys, type-to-search)
- **Command palette** of every `[ConsoleCommand]`
- **Line editor** with history, Tab complete, Ctrl+A/E/U
- **Hotkeys** listed on Help
- Direct `System.CommandLine` subcommands (`neo-tui help`, `neo-tui show block 1`, …)

## Run (from repo)

```bash
dotnet run --project src/Neo.ConsoleUI -- --config src/Neo.CLI/config.json
```

Or after build:

```bash
dotnet build src/Neo.ConsoleUI/Neo.ConsoleUI.csproj
# output: src/Neo.ConsoleUI/bin/Debug/net10.0/neo-tui.dll
dotnet src/Neo.ConsoleUI/bin/Debug/net10.0/neo-tui.dll
```

Startup flags match neo-cli: `--config`/`-c`, `--wallet`/`-w`, `--password`/`-p`, `--db-engine`, `--db-path`, `--noverify`.

Copy the `Plugins` folder from a neo-cli build next to `neo-tui` if you need plugins (RpcServer, DBFT, …). `config*.json` is copied from `Neo.CLI` on build.
