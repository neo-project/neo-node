// Copyright (C) 2015-2026 The Neo Project.
//
// InvokeResultJsonSerializer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.

using Neo.Extensions;
using Neo.Json;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins.RpcServer;

internal sealed class InvokeResultJsonSerializer(Session session, int maxSize)
{
    private int remainingSize = maxSize;

    public JArray Notifications(IEnumerable<NotifyEventArgs> notifications)
    {
        JArray result = [];
        foreach (var notification in notifications)
        {
            result.Add(new JObject
            {
                ["eventname"] = notification.EventName,
                ["contract"] = notification.ScriptHash.ToString(),
                ["state"] = ToJson(notification.State)
            });
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
                result.Add(ToJson(item));
            }
            catch (Exception ex)
            {
                result.Add("error: " + ex.Message);
            }
        }
        return result;
    }

    private JObject ToJson(StackItem item)
    {
        if (remainingSize <= 0)
            throw new InvalidOperationException("Max size reached.");

        // Reuse Neo's recursive serializer with one budget for the entire invoke result.
        var json = item.ToJson(null, ref remainingSize);
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
