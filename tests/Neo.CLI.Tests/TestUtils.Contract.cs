// Copyright (C) 2015-2026 The Neo Project.
//
// TestUtils.Contract.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.SmartContract.Manifest;

namespace Neo.CLI.Tests;

partial class TestUtils
{
    public static ContractManifest CreateDefaultManifest()
    {
        return new ContractManifest
        {
            Name = "testManifest",
            Groups = [],
            SupportedStandards = [],
            Abi = new ContractAbi
            {
                Events = [],
                Methods =
                [
                    new ContractMethodDescriptor
                    {
                        Name = "testMethod",
                        Parameters = [],
                        ReturnType = ContractParameterType.Void,
                        Offset = 0,
                        Safe = true
                    }
                ]
            },
            Permissions = [ContractPermission.DefaultPermission],
            Trusts = WildcardContainer<ContractPermissionDescriptor>.Create(),
            Extra = null
        };
    }
}
