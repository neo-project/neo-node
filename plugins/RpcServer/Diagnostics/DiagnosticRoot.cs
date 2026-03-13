// Copyright (C) 2015-2026 The Neo Project.
//
// DiagnosticRoot.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.Plugins.RpcServer.Diagnostics;

class DiagnosticRoot : IParentNode
{
    public required UInt160 ScriptHash { get; init; }
    public List<DiagnosticNode> Calls { get; } = [];
    public int ContextLoadedCount { get; set; }

    public JObject ToJson()
    {
        return new JObject
        {
            ["type"] = "root",
            ["calls"] = new JArray(Calls.Select(p => p.ToJson()))
        };
    }
}
