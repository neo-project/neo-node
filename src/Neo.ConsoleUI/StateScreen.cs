// Copyright (C) 2015-2026 The Neo Project.
//
// StateScreen.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.CLI;
using Neo.Network.P2P;
using Neo.Plugins;
using Neo.SmartContract.Native;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Diagnostics;

namespace Neo.ConsoleUI;

internal enum StatusAction
{
    Quit,
    Menu,
    CommandLine,
    Help
}

/// <summary>
/// Live dashboard matching neo-cli <c>show state</c>.
/// </summary>
internal static class StateScreen
{
    private static readonly DateTime StartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    private static int _peerScroll;
    private const int PeerPageSize = 5;

    public static StatusAction Run(MainService service)
    {
        var action = StatusAction.Quit;
        AnsiConsole.Clear();
        AnsiConsole.Live(Build(service))
            .AutoClear(true)
            .Overflow(VerticalOverflow.Crop)
            .Start(ctx =>
            {
                while (true)
                {
                    ctx.UpdateTarget(Build(service));
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        switch (key.Key)
                        {
                            case ConsoleKey.Q:
                            case ConsoleKey.Escape:
                                action = StatusAction.Quit;
                                return;
                            case ConsoleKey.M:
                            case ConsoleKey.Enter:
                                action = StatusAction.Menu;
                                return;
                            case ConsoleKey.C:
                                action = StatusAction.CommandLine;
                                return;
                            case ConsoleKey.H:
                                action = StatusAction.Help;
                                return;
                            case ConsoleKey.DownArrow:
                            case ConsoleKey.J:
                            case ConsoleKey.PageDown:
                                _peerScroll++;
                                break;
                            case ConsoleKey.UpArrow:
                            case ConsoleKey.K:
                            case ConsoleKey.PageUp:
                                _peerScroll--;
                                break;
                            case ConsoleKey.Home:
                                _peerScroll = 0;
                                break;
                            case ConsoleKey.End:
                                _peerScroll = int.MaxValue;
                                break;
                        }
                    }

                    Thread.Sleep(100);
                }
            });
        return action;
    }

    internal static IRenderable Build(MainService service)
    {
        if (Console.WindowHeight < 23 || Console.WindowWidth < 70)
        {
            return new Panel("[red]Console window too small (Need at least 70x23 visible)...[/]")
                .BorderColor(Color.Red)
                .Header("NEO NODE STATUS");
        }

        NeoSystem? system = null;
        try
        {
            system = service.NeoSystem;
        }
        catch
        {
            // Node still starting.
        }

        if (system is null)
        {
            return new Panel("[yellow]Node is starting…[/]")
                .BorderColor(Color.Yellow)
                .Header("NEO NODE STATUS");
        }

        var now = DateTime.UtcNow;
        var uptime = now - StartTime;
        uint height;
        uint headerHeight;
        int txPoolSize;
        int verified;
        int unverified;
        int connected;
        int unconnected;
        uint maxPeer;
        RemoteNode[] peers;
        try
        {
            height = NativeContract.Ledger.CurrentIndex(system.StoreView);
            headerHeight = system.HeaderCache.Last?.Index ?? height;
            txPoolSize = system.MemPool.Count;
            verified = system.MemPool.VerifiedCount;
            unverified = system.MemPool.UnVerifiedCount;
            connected = service.LocalNode.ConnectedCount;
            unconnected = service.LocalNode.UnconnectedCount;
            maxPeer = service.GetMaxPeerBlockHeight();
            peers = [.. service.LocalNode.GetRemoteNodes()];
        }
        catch (Exception ex)
        {
            return new Panel($"[red]{Markup.Escape(ex.Message)}[/]")
                .BorderColor(Color.Red)
                .Header("NEO NODE STATUS");
        }

        var memoryMb = GC.GetTotalMemory(false) / (1024 * 1024);
        var cpu = GetCpuUsage(uptime);
        var wallet = service.CurrentWallet is null ? "closed" : service.CurrentWallet.Name;
        var plugins = Plugin.Plugins.Count == 0
            ? "(none)"
            : string.Join(", ", Plugin.Plugins.Select(p => p.Name));

        var layout = new Grid();
        layout.AddColumn();
        layout.AddRow(new Rule("[bold green]NEO NODE STATUS[/]").RuleStyle("green"));

        var timeText =
            $"[grey]Current Time:[/] {now:yyyy-MM-dd HH:mm:ss}   [grey]Uptime:[/] {uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s";

        var columns = new Grid();
        columns.AddColumn();
        columns.AddColumn();
        columns.AddRow(
            MetricPanel("BLOCKCHAIN STATUS", Color.Aqua, [
                ("Block Height", $"{height,10}"),
                ("Header Height", $"{headerHeight,10}")
            ]),
            MetricPanel("SYSTEM RESOURCES", Color.Aqua, [
                ("Memory Usage", $"{memoryMb,10} MB"),
                ("CPU Usage", $"{cpu,10:F1} %")
            ]));
        columns.AddRow(
            MetricPanel("TRANSACTION POOL", TxColor(txPoolSize), [
                ("Total Txs", $"{txPoolSize,10}"),
                ("Verified Txs", $"{verified,10}"),
                ("Unverified Txs", $"{unverified,10}")
            ]),
            MetricPanel("NETWORK STATUS", Color.Green, [
                ("Connected", $"{connected,10}"),
                ("Unconnected", $"{unconnected,10}"),
                ("Max Block Height", $"{maxPeer,8}")
            ]));

        var wide = Console.WindowWidth >= 100;
        if (wide)
        {
            var head = new Table().HideHeaders().NoBorder().Expand();
            head.AddColumn(new TableColumn("t").Width(80).NoWrap().Padding(0, 0, 1, 0));
            head.AddColumn(new TableColumn("p").Padding(1, 0, 0, 0));
            head.AddRow(new Markup(timeText), Align.Center(new Markup("[green]CONNECTED PEERS[/]")));
            layout.AddRow(head);

            var split = new Table().HideHeaders().NoBorder().Expand();
            split.AddColumn(new TableColumn("metrics").Width(80).NoWrap().Padding(0, 0, 1, 0));
            split.AddColumn(new TableColumn("peers").Padding(1, 0, 0, 0));
            split.AddRow(columns, PeersPanel(peers, showTitle: false));
            layout.AddRow(split);
        }
        else
        {
            layout.AddRow(new Markup(timeText));
            layout.AddRow(columns);
            layout.AddRow(PeersPanel(peers, showTitle: true));
        }

        if (height < maxPeer && maxPeer > 0)
            layout.AddRow(SyncBar(height, maxPeer, Math.Min(70, Console.WindowWidth - 4)));

        layout.AddRow(new Markup(
            $"[grey]wallet[/] {Markup.Escape(wallet)}   [grey]plugins[/] {Markup.Escape(plugins)}"));
        layout.AddRow(new Rule(
            "[grey]M[/] menu   [grey]C[/] command line   [grey]H[/] help   [grey]Up/Down[/] peers   [grey]Q[/] quit")
            .RuleStyle("darkgreen"));
        return layout;
    }

    private static IRenderable PeersPanel(RemoteNode[] peers, bool showTitle)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Expand();
        if (showTitle)
            table.Title("[green] CONNECTED PEERS [/]");
        table.AddColumn(new TableColumn("Address").NoWrap());
        table.AddColumn(new TableColumn("Height").RightAligned().NoWrap());
        table.AddColumn(new TableColumn("Agent"));

        var ordered = peers.OrderByDescending(peer => peer.LastBlockIndex).ToArray();
        var maxScroll = Math.Max(0, ordered.Length - PeerPageSize);
        if (_peerScroll > maxScroll)
            _peerScroll = maxScroll;
        if (_peerScroll < 0)
            _peerScroll = 0;

        var page = ordered.Skip(_peerScroll).Take(PeerPageSize).ToArray();
        foreach (var peer in page)
        {
            var port = peer.ListenerTcpPort > 0 ? peer.ListenerTcpPort : peer.Remote.Port;
            var address = $"{peer.Remote.Address}:{port}";
            var agent = peer.Version?.UserAgent ?? "-";
            if (agent.Length > 36)
                agent = string.Concat(agent.AsSpan(0, 35), "…");
            table.AddRow(
                Markup.Escape(address),
                peer.LastBlockIndex.ToString(),
                Markup.Escape(agent));
        }

        if (page.Length == 0)
            table.AddRow("[grey](none)[/]", "", "");
        for (var i = page.Length == 0 ? 1 : page.Length; i < PeerPageSize; i++)
            table.AddRow(" ", " ", " ");

        if (ordered.Length > PeerPageSize)
        {
            var last = _peerScroll + page.Length;
            var more = ordered.Length > last ? "  ↓" : "";
            var less = _peerScroll > 0 ? "↑  " : "   ";
            table.Caption($"[grey]{less}{_peerScroll + 1}–{last}/{ordered.Length}{more}[/]");
        }
        else
            table.Caption($"[grey]{ordered.Length} connected[/]");

        return table;
    }

    private static Panel MetricPanel(string title, Color color, params (string Label, string Value)[] rows)
    {
        var table = new Table().HideHeaders().NoBorder().Expand();
        table.AddColumn(new TableColumn("k").PadRight(1));
        table.AddColumn(new TableColumn("v").RightAligned());
        foreach (var (label, value) in rows)
            table.AddRow($" {label}", value);
        return new Panel(table)
            .Header($" {title} ")
            .BorderColor(color)
            .Padding(1, 0, 1, 0);
    }

    private static Color TxColor(int txPoolSize)
    {
        if (txPoolSize < 100) return Color.Green;
        if (txPoolSize < 500) return Color.Yellow;
        return Color.Red;
    }

    private static Markup SyncBar(uint height, uint maxPeer, int boxWidth)
    {
        var syncPercentage = (double)height / maxPeer * 100;
        var progressBarWidth = Math.Max(10, boxWidth - 25);
        var filledWidth = (int)Math.Round(progressBarWidth * syncPercentage / 100);
        if (filledWidth > progressBarWidth) filledWidth = progressBarWidth;
        var bar = new string('█', filledWidth) + new string('░', progressBarWidth - filledWidth);
        return new Markup(
            $"[yellow]Syncing:[/] {Markup.Escape($"[{bar}]")} {syncPercentage:F2}% ({height}/{maxPeer})");
    }

    private static double GetCpuUsage(TimeSpan uptime)
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            if (uptime.TotalMilliseconds > 0 && Environment.ProcessorCount > 0)
            {
                var cpuUsage = Math.Round(currentProcess.TotalProcessorTime.TotalMilliseconds /
                    (Environment.ProcessorCount * uptime.TotalMilliseconds) * 100, 1);
                if (cpuUsage < 0) cpuUsage = 0;
                if (cpuUsage > 100) cpuUsage = 100;
                return cpuUsage;
            }
        }
        catch
        {
            // Ignore CPU usage calculation errors.
        }

        return 0;
    }
}
