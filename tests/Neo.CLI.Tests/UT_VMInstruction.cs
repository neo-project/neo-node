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
}
