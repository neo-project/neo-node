// Copyright (C) 2015-2026 The Neo Project.
//
// UT_VMInstruction.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.CLI;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.VM;

namespace Neo.CLI.Tests;

[TestClass]
public class UT_VMInstruction
{
    [TestMethod]
    public void ToString_OmitsOperandWhenNone()
    {
        var instruction = new VMInstruction(new byte[] { (byte)OpCode.NOP });
        Assert.AreEqual("0000 NOP", instruction.ToString());
    }

    [TestMethod]
    public void ToString_IncludesDecodedOperand()
    {
        var instruction = new VMInstruction(new byte[] { (byte)OpCode.PUSHINT8, 7 });
        Assert.AreEqual("0000 PUSHINT8  7", instruction.ToString());
    }

    [TestMethod]
    public void Enumerator_WalksFullScript()
    {
        var script = new byte[] { (byte)OpCode.NOP, (byte)OpCode.RET };
        var lines = new VMInstruction(script).Select(i => i.ToString()).ToArray();
        Assert.HasCount(2, lines);
        Assert.AreEqual("0000 NOP", lines[0]);
        Assert.AreEqual("0001 RET", lines[1]);
    }

    [TestMethod]
    public void DecodeOperand_CommentsReadableTextIncludingColon()
    {
        var text = "TWELVEDATA:CNY-USD"u8.ToArray();
        var instruction = new VMInstruction(PushData1(text));
        Assert.Contains(" // TWELVEDATA:CNY-USD", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_CommentsStrictUtf8PrintableRunes()
    {
        var text = "价格 café — ✓"u8.ToArray();
        var instruction = new VMInstruction(PushData1(text));
        Assert.Contains(" // 价格 café — ✓", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_RejectsInvalidUtf8AsText()
    {
        var invalid = new byte[] { 0xC0, 0x80, 0xFF };
        var instruction = new VMInstruction(PushData1(invalid));
        Assert.AreEqual($"{Convert.ToHexString(invalid)} // blob {invalid.Length} bytes", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_EscapesControlRunesInText()
    {
        var withNl = "ab\ncd"u8.ToArray();
        var instruction = new VMInstruction(PushData1(withNl));
        Assert.Contains(" // ab\\ncd", instruction.DecodeOperand());
        Assert.DoesNotContain("ab\ncd", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_FormatsUInt160()
    {
        var hash = new UInt160(Convert.FromHexString("ABCC7F51C334D4F958BE8B6C54142AC4493F0103"));
        var instruction = new VMInstruction(PushData1(hash.ToArray()));
        Assert.AreEqual($"{Convert.ToHexString(hash.ToArray())} // {hash}", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_FormatsUInt256()
    {
        var hash = new UInt256(new byte[32]);
        var instruction = new VMInstruction(PushData1(hash.ToArray()));
        Assert.AreEqual($"{Convert.ToHexString(hash.ToArray())} // {hash}", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_FormatsEcPoint()
    {
        var point = ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", ECCurve.Secp256r1);
        var encoded = point.EncodePoint(true);
        var instruction = new VMInstruction(PushData1(encoded));
        Assert.AreEqual($"{Convert.ToHexString(encoded)} // {point}", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_CommentsUnixTimestampOnPushInt32()
    {
        const int unix = 1_787_908_101;
        var script = new byte[5];
        script[0] = (byte)OpCode.PUSHINT32;
        BitConverter.GetBytes(unix).CopyTo(script, 1);
        var instruction = new VMInstruction(script);
        Assert.AreEqual("1787908101 // 2026-08-28T09:08:21Z", instruction.DecodeOperand());
    }

    [TestMethod]
    public void DecodeOperand_LeavesNonTypedPushDataAsHex()
    {
        var blob = Convert.FromHexString("71BDDFD76DBDEF67BCF1C71AE77E787B973C69F79C79FF1F");
        Assert.AreEqual(24, blob.Length);
        var instruction = new VMInstruction(PushData1(blob));
        Assert.AreEqual($"{Convert.ToHexString(blob)} // blob 24 bytes", instruction.DecodeOperand());
    }

    private static byte[] PushData1(byte[] data)
    {
        var script = new byte[2 + data.Length];
        script[0] = (byte)OpCode.PUSHDATA1;
        script[1] = (byte)data.Length;
        Buffer.BlockCopy(data, 0, script, 2, data.Length);
        return script;
    }
}
