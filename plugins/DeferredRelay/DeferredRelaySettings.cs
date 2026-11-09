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
using Neo.SmartContract.Native;
using Neo.Wallets;

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
    public uint MaxTransactionsPerSender { get; }
    public long MinNetworkFee { get; }
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
            section.GetValue("MaxTransactionsPerSender", 0u),
            GetGasAmount(section, "MinNetworkFee", 0L),
            section.GetValue("CheckFrequency", 0u),
            section.GetValue("UnhandledExceptionPolicy", UnhandledExceptionPolicy.Ignore))
    {
    }

    private DeferredRelaySettings(string path, uint maxTransactions, uint maxTransactionsPerSender, long minNetworkFee, uint checkFrequency, UnhandledExceptionPolicy exceptionPolicy)
    {
        Validate(maxTransactions, maxTransactionsPerSender);
        Path = path;
        MaxTransactions = maxTransactions;
        MaxTransactionsPerSender = maxTransactionsPerSender;
        MinNetworkFee = minNetworkFee;
        CheckFrequency = checkFrequency;
        ExceptionPolicy = exceptionPolicy;
    }

    /// <summary>
    /// When <see cref="MaxTransactionsPerSender"/> is non-zero it must be strictly less than
    /// <see cref="MaxTransactions"/> so a single sender cannot monopolize the whole queue cap.
    /// </summary>
    internal static void Validate(uint maxTransactions, uint maxTransactionsPerSender)
    {
        if (maxTransactionsPerSender == 0 || maxTransactions == 0)
            return;
        if (maxTransactionsPerSender >= maxTransactions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTransactionsPerSender),
                maxTransactionsPerSender,
                $"{nameof(MaxTransactionsPerSender)} must be less than {nameof(MaxTransactions)} when both are non-zero.");
        }
    }

    public static void Load(IConfigurationSection section) =>
        Default = new DeferredRelaySettings(section);

    internal static DeferredRelaySettings Create(uint maxTransactions, uint checkFrequency, uint maxTransactionsPerSender = 0u, long minNetworkFee = 0L) =>
        new("DeferredRelay_{0}", maxTransactions, maxTransactionsPerSender, minNetworkFee, checkFrequency, UnhandledExceptionPolicy.Ignore);

    /// <summary>
    /// Parses a GAS-denominated decimal from plugin configuration into datoshi (RpcServer <c>GetGasAmount</c>).
    /// </summary>
    private static long GetGasAmount(IConfigurationSection section, string key, long defaultValue)
    {
        if (!section.GetSection(key).Exists()) return defaultValue;
        return (long)new BigDecimal(section.GetValue<decimal>(key), NativeContract.GAS.Decimals).Value;
    }
}
