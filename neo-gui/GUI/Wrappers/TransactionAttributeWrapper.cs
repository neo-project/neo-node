// Copyright (C) 2014-2021 NEO GLOBAL DEVELOPMENT.
// 
// The neo-consolservice is free software distributed under the MIT software license, 
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace Neo.GUI.Wrappers
{
    internal class TransactionAttributeWrapper
    {
        public TransactionAttributeType Usage { get; set; }
        [TypeConverter(typeof(HexConverter))]
        public byte[] Data { get; set; }

        public TransactionAttribute Unwrap()
        {
            using var reader = new BinaryReader(new MemoryStream(Data), Encoding.UTF8, false);

            return TransactionAttribute.DeserializeFrom(reader);
        }
    }
}
