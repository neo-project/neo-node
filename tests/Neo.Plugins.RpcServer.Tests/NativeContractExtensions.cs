// Copyright (C) 2015-2026 The Neo Project.
//
// NativeContractExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions.IO;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace Neo.Plugins.RpcServer.Tests;

public static class NativeContractExtensions
{
    public static void AddContract(this DataCache snapshot, UInt160 hash, ContractState state)
    {
        //key: hash, value: ContractState
        var key = new KeyBuilder(NativeContract.ContractManagement.Id, 8).Add(hash);
        snapshot.Add(key, new StorageItem(state));
        //key: id, value: hash
        var key2 = new KeyBuilder(NativeContract.ContractManagement.Id, 12).Add(state.Id);
        if (!snapshot.Contains(key2)) snapshot.Add(key2, new StorageItem(hash.ToArray()));
    }

    public static void DeleteContract(this DataCache snapshot, UInt160 hash)
    {
        //key: hash, value: ContractState
        var key = new KeyBuilder(NativeContract.ContractManagement.Id, 8).Add(hash);
        var value = snapshot.TryGet(key)?.GetInteroperable<ContractState>();
        snapshot.Delete(key);
        if (value != null)
        {
            //key: id, value: hash
            var key2 = new KeyBuilder(NativeContract.ContractManagement.Id, 12).Add(value.Id);
            snapshot.Delete(key2);
        }
    }
}
