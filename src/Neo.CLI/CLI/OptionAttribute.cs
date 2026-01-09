// Copyright (C) 2015-2026 The Neo Project.
//
// OptionAttribute.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.CLI;

[AttributeUsage(AttributeTargets.Property)]
class OptionAttribute(string name, params string[] aliases) : Attribute
{
    public string Name => name;
    public string[] Aliases => aliases;
    public string? Description { get; init; }
}
