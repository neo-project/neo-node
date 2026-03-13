// Copyright (C) 2015-2026 The Neo Project.
//
// DynamicScriptInfo.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.VM.Types;

namespace Neo.Plugins.RpcServer.Diagnostics;

class DynamicScriptInfo : DiagnosticNode, ICanReturn, IParentNode
{
    public required UInt160 ScriptHash { get; init; }
    public List<StackItem> Arguments { get; } = [];
    public StackItem? ReturnValue { get; set; }
    public required IParentNode Caller { get; init; }
    public List<DiagnosticNode> Calls { get; } = [];
    public int ContextLoadedCount { get; set; }

    public override JObject ToJson()
    {
        return new JObject
        {
            ["type"] = "dynamic",
            ["hash"] = ScriptHash.ToString(),
            ["args"] = new JArray(Arguments.Select(p => p.ToJson())),
            ["return"] = ReturnValue?.ToJson(),
            ["calls"] = new JArray(Calls.Select(p => p.ToJson()))
        };
    }
}
