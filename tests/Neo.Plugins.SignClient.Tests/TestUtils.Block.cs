// Copyright (C) 2015-2025 The Neo Project.
//
// TestUtils.Block.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Runtime.CompilerServices;

namespace Neo.Plugins.SignClient.Tests;

public partial class TestUtils
{
    const byte Prefix_Block = 5;
    const byte Prefix_BlockHash = 9;
    const byte Prefix_Transaction = 11;
    const byte Prefix_CurrentBlock = 12;

    /// <summary>
    /// Test Util function MakeHeader
    /// </summary>
    /// <param name="snapshot">The snapshot of the current storage provider. Can be null.</param>
    /// <param name="prevHash">The previous block hash</param>
    public static Header MakeHeader(DataCache snapshot, UInt256 prevHash)
    {
        return new Header
        {
            PrevHash = prevHash,
            MerkleRoot = UInt256.Parse("0x6226416a0e5aca42b5566f5a19ab467692688ba9d47986f6981a7f747bba2772"),
            Timestamp = new DateTime(2024, 06, 05, 0, 33, 1, 001, DateTimeKind.Utc).ToTimestampMS(),
            Index = snapshot != null ? NativeContract.Ledger.CurrentIndex(snapshot) + 1 : 0,
            Nonce = 0,
            NextConsensus = UInt160.Zero,
            Witness = new Witness
            {
                InvocationScript = ReadOnlyMemory<byte>.Empty,
                VerificationScript = new[] { (byte)OpCode.PUSH1 }
            }
        };
    }

    public static Block MakeBlock(DataCache snapshot, UInt256 prevHash, int numberOfTransactions)
    {
        var block = (Block)RuntimeHelpers.GetUninitializedObject(typeof(Block));
        var header = MakeHeader(snapshot, prevHash);
        var transactions = new Transaction[numberOfTransactions];
        if (numberOfTransactions > 0)
        {
            for (var i = 0; i < numberOfTransactions; i++)
            {
                transactions[i] = GetTransaction(UInt160.Zero);
            }
        }

        block.Header = header;
        block.Transactions = transactions;
        header.MerkleRoot = MerkleTree.ComputeRoot(block.Transactions.Select(p => p.Hash).ToArray());
        return block;
    }
}
