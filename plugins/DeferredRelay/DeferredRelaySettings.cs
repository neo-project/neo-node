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
    public string Path { get; }
    public uint Network { get; }
    public uint MaxTransactions { get; }
    public uint CheckFrequency { get; }
    public UnhandledExceptionPolicy ExceptionPolicy { get; }

    public static DeferredRelaySettings Default { get; private set; } = default!;

    public bool Enabled => MaxTransactions > 0 && CheckFrequency > 0;

    private DeferredRelaySettings(IConfigurationSection section)
        : this(
            section.GetValue("Path", "DeferredRelay_{0}")!,
            section.GetValue("Network", 860833102u),
            section.GetValue("MaxTransactions", 0u),
            section.GetValue("CheckFrequency", 0u),
            section.GetValue("UnhandledExceptionPolicy", UnhandledExceptionPolicy.Ignore))
    {
    }

    private DeferredRelaySettings(string path, uint network, uint maxTransactions, uint checkFrequency, UnhandledExceptionPolicy exceptionPolicy)
    {
        Path = path;
        Network = network;
        MaxTransactions = maxTransactions;
        CheckFrequency = checkFrequency;
        ExceptionPolicy = exceptionPolicy;
    }

    public static void Load(IConfigurationSection section) =>
        Default = new DeferredRelaySettings(section);

    internal static DeferredRelaySettings Create(uint network, uint maxTransactions, uint checkFrequency) =>
        new("DeferredRelay_{0}", network, maxTransactions, checkFrequency, UnhandledExceptionPolicy.Ignore);
}
