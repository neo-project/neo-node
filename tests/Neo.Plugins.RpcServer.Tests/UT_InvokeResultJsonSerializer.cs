// Copyright (C) 2015-2026 The Neo Project.
//
// UT_InvokeResultJsonSerializer.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using VMArray = Neo.VM.Types.Array;

namespace Neo.Plugins.RpcServer.Tests;

public partial class UT_RpcServer
{
    [TestMethod]
    public void TestInvokeResultJsonSerializerPreservesPerItemLimit()
    {
        using var scriptBuilder = new ScriptBuilder();
        scriptBuilder.EmitPush(new byte[80]);
        scriptBuilder.Emit(OpCode.DUP);
        using var session = new Session(_neoSystem, scriptBuilder.ToArray(), null, null, _rpcServerSettings.MaxGasInvoke, null);

        var serializer = new InvokeResultJsonSerializer(session, 150, int.MaxValue, int.MaxValue);
        var stack = serializer.ResultStack(session.Engine.ResultStack);

        Assert.HasCount(2, stack);
        Assert.AreEqual(nameof(ByteString), stack[0]["type"].AsString());
        Assert.AreEqual(nameof(ByteString), stack[1]["type"].AsString());
    }

    [TestMethod]
    public void TestInvokeResultJsonSerializerKeepsNotificationsAndStackSeparate()
    {
        using var scriptBuilder = new ScriptBuilder();
        scriptBuilder.EmitPush(new byte[80]);
        using var session = new Session(_neoSystem, scriptBuilder.ToArray(), null, null, _rpcServerSettings.MaxGasInvoke, null);
        var notification = new NotifyEventArgs(null, UInt160.Zero, "test", new VMArray([new ByteString(new byte[40])]));

        var serializer = new InvokeResultJsonSerializer(session, 150, 150, 150);
        var notifications = serializer.Notifications([notification]);
        var stack = serializer.ResultStack(session.Engine.ResultStack);

        Assert.HasCount(1, notifications);
        Assert.AreEqual("test", notifications[0]["eventname"].AsString());
        Assert.AreEqual(nameof(ByteString), stack[0]["type"].AsString());
    }

    [TestMethod]
    public void TestInvokeResultJsonSerializerSharesBudgetAcrossNotifications()
    {
        using var scriptBuilder = new ScriptBuilder();
        scriptBuilder.Emit(OpCode.RET);
        using var session = new Session(_neoSystem, scriptBuilder.ToArray(), null, null, _rpcServerSettings.MaxGasInvoke, null);
        var notification = new NotifyEventArgs(null, UInt160.Zero, "test", new VMArray([new ByteString(new byte[40])]));

        var serializer = new InvokeResultJsonSerializer(session, 150, int.MaxValue, 150);

        Assert.ThrowsExactly<InvalidOperationException>(() => serializer.Notifications([notification, notification]));
    }

    [TestMethod]
    public void TestInvokeResultJsonSerializerFailedItemDoesNotConsumeStackBudget()
    {
        using var scriptBuilder = new ScriptBuilder();
        scriptBuilder.EmitPush(new byte[60]);
        scriptBuilder.EmitPush(new byte[80]);
        scriptBuilder.EmitPush(1);
        using var session = new Session(_neoSystem, scriptBuilder.ToArray(), null, null, _rpcServerSettings.MaxGasInvoke, null);

        var serializer = new InvokeResultJsonSerializer(session, 150, 150, int.MaxValue);
        var stack = serializer.ResultStack(session.Engine.ResultStack);

        Assert.HasCount(3, stack);
        Assert.AreEqual(nameof(ByteString), stack[0]["type"].AsString());
        Assert.AreEqual("error: Max size reached.", stack[1].AsString());
        Assert.AreEqual(nameof(Integer), stack[2]["type"].AsString());
    }
}
