// Copyright (C) 2015-2026 The Neo Project.
//
// DeferredRelaySettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.DeferredRelay;

internal sealed class DeferredRelaySettings : IPluginSettings
{
    /// <summary>
    /// Default upper bound for the number of in-window transactions relayed per
    /// <see cref="DeferredRelayEngine.ProcessQueuedAsync"/> cycle. Matches the
    /// same order of magnitude as Neo's typical <c>MaxTransactionsPerBlock</c> so a
    /// single drain pass cannot monopolize the Blockchain actor mailbox.
    /// </summary>
    public const uint DefaultMaxRelayPerCycle = 256u;

    public string Path { get; }
    public uint MaxTransactions { get; }
    public uint CheckFrequency { get; }
    public UnhandledExceptionPolicy ExceptionPolicy { get; }

    public static DeferredRelaySettings Default { get; private set; } = default!;

    public bool Enabled => MaxTransactions > 0 && CheckFrequency > 0;

    /// <summary>
    /// Effective per-cycle relay cap. Capped at <see cref="DefaultMaxRelayPerCycle"/>;
    /// for small queues (<see cref="MaxTransactions"/> &lt; cap) the value collapses to
    /// <see cref="MaxTransactions"/> so a single cycle can still drain the whole queue.
    /// </summary>
    public uint MaxRelayPerCycle => Math.Min(MaxTransactions, DefaultMaxRelayPerCycle);

    private DeferredRelaySettings(IConfigurationSection section)
        : this(
            section.GetValue("Path", "DeferredRelay_{0}")!,
            section.GetValue("MaxTransactions", 0u),
            section.GetValue("CheckFrequency", 0u),
            section.GetValue("UnhandledExceptionPolicy", UnhandledExceptionPolicy.Ignore))
    {
    }

    private DeferredRelaySettings(string path, uint maxTransactions, uint checkFrequency, UnhandledExceptionPolicy exceptionPolicy)
    {
        Path = path;
        MaxTransactions = maxTransactions;
        CheckFrequency = checkFrequency;
        ExceptionPolicy = exceptionPolicy;
    }

    public static void Load(IConfigurationSection section) =>
        Default = new DeferredRelaySettings(section);

    internal static DeferredRelaySettings Create(uint maxTransactions, uint checkFrequency) =>
        new("DeferredRelay_{0}", maxTransactions, checkFrequency, UnhandledExceptionPolicy.Ignore);
}
