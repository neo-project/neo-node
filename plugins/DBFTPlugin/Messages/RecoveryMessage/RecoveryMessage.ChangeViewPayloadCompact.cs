// Copyright (C) 2015-2026 The Neo Project.
//
// RecoveryMessage.ChangeViewPayloadCompact.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.Plugins.DBFTPlugin.Types;

namespace Neo.Plugins.DBFTPlugin.Messages;

partial class RecoveryMessage
{
    public class ChangeViewPayloadCompact : ISerializable
    {
        public byte ValidatorIndex;
        public byte OriginalViewNumber;
        public ulong Timestamp;
        public ReadOnlyMemory<byte> InvocationScript;
        public ChangeViewReason Reason;
        public UInt256[] RejectedHashes;

        int ISerializable.Size =>
            sizeof(byte) +                  //ValidatorIndex
            sizeof(byte) +                  //OriginalViewNumber
            sizeof(ulong) +                 //Timestamp
            InvocationScript.GetVarSize() + //InvocationScript
            sizeof(ChangeViewReason) +      //Reason
            (Reason switch
            {
                ChangeViewReason.TxRejectedByPolicy or ChangeViewReason.TxInvalid => RejectedHashes.GetVarSize(),
                _ => 0
            });

        void ISerializable.Deserialize(ref MemoryReader reader)
        {
            ValidatorIndex = reader.ReadByte();
            OriginalViewNumber = reader.ReadByte();
            Timestamp = reader.ReadUInt64();
            InvocationScript = reader.ReadVarMemory(1024);
            Reason = (ChangeViewReason)reader.ReadByte();
            if (Reason is ChangeViewReason.TxRejectedByPolicy or ChangeViewReason.TxInvalid)
                RejectedHashes = reader.ReadSerializableArray<UInt256>(ushort.MaxValue);
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(ValidatorIndex);
            writer.Write(OriginalViewNumber);
            writer.Write(Timestamp);
            writer.WriteVarBytes(InvocationScript.Span);
            writer.Write((byte)Reason);
            if (Reason is ChangeViewReason.TxRejectedByPolicy or ChangeViewReason.TxInvalid)
                writer.Write(RejectedHashes);
        }
    }
}
