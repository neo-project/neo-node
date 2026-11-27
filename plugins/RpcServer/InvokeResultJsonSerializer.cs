// Copyright (C) 2015-2026 The Neo Project.
//
// InvokeResultJsonSerializer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins.RpcServer;

internal sealed class InvokeResultJsonSerializer(Session session, int maxItemSize, int maxStackSize, int maxNotificationsSize)
{
    private int remainingStackSize = maxStackSize;
    private int remainingNotificationsSize = maxNotificationsSize;

    public JArray Notifications(IEnumerable<NotifyEventArgs> notifications)
    {
        JArray result = [];
        foreach (var notification in notifications)
        {
            try
            {
                result.Add(new JObject
                {
                    ["eventname"] = notification.EventName,
                    ["contract"] = notification.ScriptHash.ToString(),
                    ["state"] = ToJson(notification.State, ref remainingNotificationsSize)
                });
            }
            catch (Exception ex)
            {
                result.Add("error: " + ex.Message);
            }
        }
        return result;
    }

    public JArray ResultStack(EvaluationStack stack)
    {
        JArray result = [];
        foreach (var item in stack)
        {
            try
            {
                result.Add(ToJson(item, ref remainingStackSize));
            }
            catch (Exception ex)
            {
                result.Add("error: " + ex.Message);
            }
        }
        return result;
    }

    private JObject ToJson(StackItem item, ref int remainingSize)
    {
        if (remainingSize <= 0)
            throw new InvalidOperationException("Max size reached.");

        // Only commit the aggregate budget after a successful serialization.
        var itemBudget = Math.Min(maxItemSize, remainingSize);
        var available = itemBudget;
        var json = item.ToJson(null, ref available);
        remainingSize -= itemBudget - available;
        if (item is InteropInterface interopInterface && interopInterface.GetInterface<object>() is IIterator iterator)
        {
            Guid id = Guid.NewGuid();
            session.Iterators.Add(id, iterator);
            json["interface"] = nameof(IIterator);
            json["id"] = id.ToString();
        }
        return json;
    }
}
