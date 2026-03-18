// Copyright (C) 2015-2026 The Neo Project.
//
// SyscallInfo.cs file belongs to the neo project and is free
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

class SyscallInfo : DiagnosticNode
{
    public required string Name { get; init; }
    public List<StackItem> Arguments { get; } = [];
    public StackItem? Result { get; set; }

    public override JObject ToJson()
    {
        return new JObject
        {
            ["type"] = "syscall",
            ["name"] = Name,
            ["args"] = new JArray(Arguments.Select(p => p.ToJson())),
            ["result"] = Result?.ToJson()
        };
    }
}
