// Copyright (C) 2015-2026 The Neo Project.
//
// MinimumRequiredFeeReader.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Reflection;

namespace Neo.Plugins.RpcServer;

/// <summary>
/// Caches an optional runtime capability without treating malformed fee data as
/// absence. The published core need not expose this preview capability.
/// </summary>
internal sealed class MinimumRequiredFeeReader<TEngine>
{
    private readonly Func<TEngine, long>? getter;

    internal MinimumRequiredFeeReader()
    {
        var property = typeof(TEngine).GetProperty("MinimumRequiredFee",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (property is null) return;
        var method = property.GetGetMethod();
        if (property.PropertyType != typeof(long) || property.GetIndexParameters().Length != 0 || method is null || method.IsStatic)
            throw new InvalidOperationException("MinimumRequiredFee must be a public instance long getter.");
        getter = method.CreateDelegate<Func<TEngine, long>>();
    }

    internal long? Read(TEngine engine, long consumedFee)
    {
        if (getter is null) return null;
        long fee = getter(engine);
        if (fee < 0 || fee < consumedFee)
            throw new InvalidOperationException("MinimumRequiredFee must be nonnegative and at least the consumed fee.");
        return fee;
    }
}
