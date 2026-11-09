// Copyright (C) 2015-2026 The Neo Project.
//
// UT_MaxLength.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;

namespace Neo.Cryptography.MPTTrie.Tests;

[TestClass]
public class UT_MaxLength
{
    [TestMethod]
    public void TestMaxLength()
    {
        static byte[] MaliciousEntry(int depth)
        {
            var entry = new byte[(depth * 3) + 1];
            var offset = 0;

            for (var i = 0; i < depth; i++)
            {
                entry[offset++] = 0x01;
                entry[offset++] = 0x00;
            }

            entry[offset++] = 0x04;

            for (var i = 0; i < depth; i++)
                entry[offset++] = 0x00;

            return entry;
        }

        var entry = MaliciousEntry(10000);
        var root = new UInt256(Crypto.Hash256(entry));
        Assert.ThrowsExactly<FormatException>(() =>
            Trie.VerifyProof(root, [0x00], new HashSet<byte[]>([entry], ByteArrayEqualityComparer.Default)));
    }
}
