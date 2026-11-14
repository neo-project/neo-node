// Copyright (C) 2015-2026 The Neo Project.
//
// Node.Leaf.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.VM;

namespace Neo.Cryptography.MPTTrie;

partial class Node
{
    // Native contract state can reach the VM item limit, beyond System.Storage.Put's limit.
    // Include the variable-length prefix and legacy StorageItem flag.
    public static readonly int MaxValueLength = checked((int)ExecutionEngineLimits.Default.MaxItemSize
        + ExecutionEngineLimits.Default.MaxItemSize.GetVarSize() + sizeof(bool));
    public ReadOnlyMemory<byte> Value { get; set; } = ReadOnlyMemory<byte>.Empty;

    public static Node NewLeaf(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Node
        {
            Type = NodeType.LeafNode,
            Value = value,
            Reference = 1,
        };
    }

    protected int LeafSize => Value.GetVarSize();

    private void SerializeLeaf(BinaryWriter writer)
    {
        writer.WriteVarBytes(Value.Span);
    }

    private void DeserializeLeaf(ref MemoryReader reader)
    {
        Value = reader.ReadVarMemory();
    }
}
