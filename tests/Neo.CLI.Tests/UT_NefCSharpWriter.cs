// Copyright (C) 2015-2026 The Neo Project.
//
// UT_NefCSharpWriter.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.CLI;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.CLI.Tests;

[TestClass]
public class UT_NefCSharpWriter
{
    [TestMethod]
    public void ToClassName_SanitizesFileName()
    {
        Assert.AreEqual("My_Contract", NefCSharpWriter.ToClassName(@"C:\tmp\My-Contract.nef"));
        Assert.AreEqual("_1feed", NefCSharpWriter.ToClassName("1feed.nef"));
        Assert.AreEqual("__", NefCSharpWriter.ToClassName("..."));
    }

    [TestMethod]
    public void Generate_EmitsPushAndReturn()
    {
        var nef = CreateNef(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET });
        var csharp = NefCSharpWriter.Generate(nef, "Sample");
        Assert.Contains("public static class Sample", csharp);
        Assert.Contains("Compiler: test-compiler", csharp);
        Assert.Contains("L_0000:", csharp);
        Assert.Contains("stack.Push(1);", csharp);
        Assert.Contains("L_0001:", csharp);
        Assert.Contains("return;", csharp);
        Assert.Contains("static bool IsTrue", csharp);
    }

    [TestMethod]
    public void Generate_EmitsJumpAndStringPush()
    {
        // JMP +1 would skip; use JMP_L-style relative: PUSHDATA1 "hi" then RET
        var script = new byte[]
        {
            (byte)OpCode.PUSHDATA1, 2, (byte)'h', (byte)'i',
            (byte)OpCode.RET,
        };
        var csharp = NefCSharpWriter.Generate(CreateNef(script), "Strings");
        Assert.Contains("stack.Push(\"hi\");", csharp);
        Assert.Contains("return;", csharp);
    }

    [TestMethod]
    public void Generate_JmpIfUsesGotoLabel()
    {
        // PUSH1, JMPIF +offset to RET
        // JMPIF is 2 bytes (opcode + sbyte). Position 1, offset to position 3 (RET)
        // instructions: 0 PUSH1, 1 JMPIF, 3 RET. offset = 3-1 = 2
        var script = new byte[]
        {
            (byte)OpCode.PUSH1,
            (byte)OpCode.JMPIF, 2,
            (byte)OpCode.RET,
        };
        var csharp = NefCSharpWriter.Generate(CreateNef(script), "Jumps");
        Assert.Contains("if (IsTrue(stack.Pop())) goto L_0003;", csharp);
    }

    private static NefFile CreateNef(byte[] script)
    {
        var nef = new NefFile
        {
            Compiler = "test-compiler",
            Source = "",
            Tokens = [],
            Script = script,
            CheckSum = 0,
        };
        nef.CheckSum = NefFile.ComputeChecksum(nef);
        return nef;
    }
}
