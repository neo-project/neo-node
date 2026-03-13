// Copyright (C) 2015-2026 The Neo Project.
//
// Diagnostic.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System.Diagnostics;
using ExecutionContext = Neo.VM.ExecutionContext;

namespace Neo.Plugins.RpcServer.Diagnostics;

class Diagnostic : IDiagnostic
{
    private ApplicationEngine engine = null!;
    private IParentNode? currentNode;

    public DiagnosticRoot? Root { get; private set; }

    public void Initialized(ApplicationEngine engine)
    {
        this.engine = engine;
    }

    public void Disposed() { }

    public void ContextLoaded(ExecutionContext context)
    {
        currentNode ??= Root = new DiagnosticRoot
        {
            ScriptHash = context.GetState<ExecutionContextState>().ScriptHash!
        };
        currentNode.ContextLoadedCount++;
    }

    public void ContextUnloaded(ExecutionContext context)
    {
        currentNode!.ContextLoadedCount--;
        if (currentNode.ContextLoadedCount == 0)
            currentNode = currentNode.Caller;
    }

    public void PreExecuteInstruction(Instruction instruction)
    {
        try
        {
            switch (instruction.OpCode)
            {
                case OpCode.CALL:
                    OnCall(instruction, engine.CurrentContext!.InstructionPointer + instruction.TokenI8);
                    break;
                case OpCode.CALL_L:
                    OnCall(instruction, engine.CurrentContext!.InstructionPointer + instruction.TokenI32);
                    break;
                case OpCode.CALLA:
                    OnCallA(instruction);
                    break;
                case OpCode.CALLT:
                    OnCallT(instruction);
                    break;
                case OpCode.SYSCALL:
                    OnSyscall(instruction);
                    break;
                case OpCode.INITSLOT:
                    OnInitSlot(instruction);
                    break;
                case OpCode.RET:
                    OnRet(instruction);
                    break;
            }
        }
        catch
        {
            Debug.WriteLine($"Diagnostic: failed to handle instruction {instruction.OpCode} at position {engine.CurrentContext!.InstructionPointer}");
        }
    }

    public void PostExecuteInstruction(Instruction instruction)
    {
        try
        {
            if (instruction.OpCode == OpCode.SYSCALL)
                PostSyscall(instruction);
        }
        catch
        {
        }
    }

    void OnCall(Instruction instruction, int position)
    {
        var call = new InternalCallInfo
        {
            Instruction = instruction,
            Position = position,
            Caller = currentNode!
        };
        currentNode!.Calls.Add(call);
        currentNode = call;
    }

    void OnCallA(Instruction instruction)
    {
        if (engine.Peek() is Pointer pointer)
        {
            OnCall(instruction, pointer.Position);
        }
    }

    void OnCallT(Instruction instruction)
    {
        ContractState contract = engine.CurrentContext!.GetState<ExecutionContextState>().Contract!;
        MethodToken token = contract.Nef.Tokens[instruction.TokenU16];
        var call = new InvocationInfo
        {
            Instruction = instruction,
            ScriptHash = token.Hash,
            Method = token.Method,
            Caller = currentNode!
        };
        for (int i = 0; i < token.ParametersCount; i++)
            call.Arguments.Add(engine.Peek(i).DeepCopy(true));
        currentNode!.Calls.Add(call);
        currentNode = call;
    }

    void OnSyscall(Instruction instruction)
    {
        var interop = ApplicationEngine.GetInteropDescriptor(instruction.TokenU32);
        switch (interop.Name)
        {
            case "System.Contract.Call":
                OnCallContract(instruction, interop);
                return;
            case "System.Contract.CallNative":
                OnCallNative(instruction, interop);
                return;
            case "System.Runtime.LoadScript":
                OnLoadScript(instruction, interop);
                return;
        }
        var call = new SyscallInfo
        {
            Instruction = instruction,
            Name = interop.Name
        };
        for (int i = 0; i < interop.Parameters.Count; i++)
            call.Arguments.Add(engine.Peek(i).DeepCopy(true));
        currentNode!.Calls.Add(call);
    }

    void PostSyscall(Instruction instruction)
    {
        if (currentNode!.Calls.Count == 0) return;
        if (currentNode.Calls[^1] is SyscallInfo call)
        {
            var interop = ApplicationEngine.GetInteropDescriptor(instruction.TokenU32);
            if (interop.Handler.ReturnType != typeof(void))
                call.Result = engine.Peek().DeepCopy(true);
        }
    }

    void OnInitSlot(Instruction instruction)
    {
        if (currentNode is InternalCallInfo call)
        {
            for (var i = 0; i < instruction.TokenU8_1; i++)
            {
                call.Arguments.Add(engine.Peek(i).DeepCopy(true));
            }
        }
    }

    void OnRet(Instruction instruction)
    {
        if (currentNode is ICanReturn call)
        {
            var stack = engine.CurrentContext!.EvaluationStack;
            if (stack.Count > 0)
                call.ReturnValue = stack.Peek().DeepCopy(true);
        }
    }

    void OnCallContract(Instruction instruction, InteropDescriptor interop)
    {
        UInt160 contractHash = new(engine.Peek(0).GetSpan());
        string method = engine.Peek(1).GetString()!;
        var args = (VM.Types.Array)engine.Peek(3);
        var call = new InvocationInfo
        {
            Instruction = instruction,
            ScriptHash = contractHash,
            Method = method,
            Caller = currentNode!
        };
        foreach (var arg in args)
            call.Arguments.Add(arg.DeepCopy(true));
        currentNode!.Calls.Add(call);
        currentNode = call;
    }

    void OnCallNative(Instruction instruction, InteropDescriptor interop)
    {
        if (currentNode is InvocationInfo call)
        {
            call.IsNative = true;
        }
    }

    void OnLoadScript(Instruction instruction, InteropDescriptor interop)
    {
        ReadOnlySpan<byte> script = engine.Peek(0).GetSpan();
        var args = (VM.Types.Array)engine.Peek(2);
        var call = new DynamicScriptInfo
        {
            Instruction = instruction,
            ScriptHash = script.ToScriptHash(),
            Caller = currentNode!
        };
        foreach (var arg in args)
            call.Arguments.Add(arg.DeepCopy(true));
        currentNode!.Calls.Add(call);
        currentNode = call;
    }
}
