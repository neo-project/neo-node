// Copyright (C) 2015-2026 The Neo Project.
//
// PrepareRequest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.DBFTPlugin.Types;

namespace Neo.Plugins.DBFTPlugin.Messages;

public class PrepareRequest : ConsensusMessage
{
    public uint Version;
    public UInt256 PrevHash;
    public ulong Timestamp;
    public ulong Nonce;
    public UInt256[] TransactionHashes;
    internal bool Extended;
    public Transaction[] Transactions;

    public override int Size => base.Size
        + sizeof(uint)                      //Version
        + UInt256.Length                    //PrevHash
        + sizeof(ulong)                     //Timestamp
        + sizeof(ulong)                     // Nonce
        + (Extended ? Transactions.GetVarSize() : TransactionHashes.GetVarSize());   //TransactionHashes or Transactions

    public PrepareRequest() : base(ConsensusMessageType.PrepareRequest) { }

    public override void Deserialize(ref MemoryReader reader)
    {
        base.Deserialize(ref reader);
        Extended = IsExtended?.Invoke(BlockIndex) ?? false;
        Version = reader.ReadUInt32();
        PrevHash = reader.ReadSerializable<UInt256>();
        Timestamp = reader.ReadUInt64();
        Nonce = reader.ReadUInt64();
        if (Extended)
        {
            Transactions = reader.ReadSerializableArray<Transaction>(ushort.MaxValue);
            if (Transactions.Select(p => p.Hash).Distinct().Count() != Transactions.Length)
                throw new FormatException("Transactions are duplicate");
        }
        else
        {
            TransactionHashes = reader.ReadSerializableArray<UInt256>(ushort.MaxValue);
            if (TransactionHashes.Distinct().Count() != TransactionHashes.Length)
                throw new FormatException($"Transaction hashes are duplicate");
        }
    }

    public override bool Verify(ProtocolSettings protocolSettings)
    {
        if (!base.Verify(protocolSettings)) return false;
        return (Extended ? Transactions.Length : TransactionHashes.Length) <= protocolSettings.MaxTransactionsPerBlock;
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(Version);
        writer.Write(PrevHash);
        writer.Write(Timestamp);
        writer.Write(Nonce);
        if (Extended)
        {
            writer.Write(Transactions);
        }
        else
        {
            writer.Write(TransactionHashes);
        }
    }

    public UInt256[] GetTransactionHashes()
    {
        return Extended ? Transactions.Select(p => p.Hash).ToArray() : TransactionHashes;
    }
}
