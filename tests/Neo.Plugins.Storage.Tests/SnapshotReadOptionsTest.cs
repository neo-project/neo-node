// Copyright (C) 2015-2026 The Neo Project.
//
// SnapshotReadOptionsTest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Reflection;

namespace Neo.Plugins.Storage.Tests;

[TestClass]
public class SnapshotReadOptionsTest
{
    [TestMethod]
    public void LevelDbSnapshotUsesDedicatedReadOptionsForScanAndPointReads()
    {
        var snapshotType = GetSnapshotType(typeof(LevelDBStore));
        var scanReadOptions = GetField(snapshotType, "_scanReadOptions");
        var pointReadOptions = GetField(snapshotType, "_pointReadOptions");

        AssertMethodUsesOnly(snapshotType, "Find", scanReadOptions, pointReadOptions);
        AssertMethodUsesOnly(snapshotType, "GetEnumerator", scanReadOptions, pointReadOptions);
        AssertMethodUsesOnly(snapshotType, "Contains", pointReadOptions, scanReadOptions);
        AssertMethodUsesOnly(snapshotType, "TryGet", pointReadOptions, scanReadOptions);
    }

    [TestMethod]
    public void RocksDbSnapshotUsesDedicatedReadOptionsForScanAndPointReads()
    {
        var snapshotType = GetSnapshotType(typeof(RocksDBStore));
        var scanOptions = GetField(snapshotType, "_scanOptions");
        var pointOptions = GetField(snapshotType, "_pointOptions");

        AssertMethodUsesOnly(snapshotType, "Find", scanOptions, pointOptions);
        AssertMethodUsesOnly(snapshotType, "Contains", pointOptions, scanOptions);
        AssertMethodUsesOnly(snapshotType, "TryGet", pointOptions, scanOptions);
    }

    private static Type GetSnapshotType(Type storeProviderType)
    {
        return storeProviderType.Assembly.GetType("Neo.Plugins.Storage.Snapshot", throwOnError: true)!;
    }

    private static FieldInfo GetField(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"{type.FullName} should declare {fieldName}.");
        return field;
    }

    private static void AssertMethodUsesOnly(Type type, string methodName, FieldInfo expectedField, FieldInfo unexpectedField)
    {
        var implementations = GetImplementations(type, methodName).ToArray();
        Assert.IsNotEmpty(implementations, $"{type.FullName}.{methodName} should exist.");
        Assert.IsTrue(
            implementations.Any(method => UsesField(method, expectedField)),
            $"{type.FullName}.{methodName} should use {expectedField.Name}.");
        Assert.IsFalse(
            implementations.Any(method => UsesField(method, unexpectedField)),
            $"{type.FullName}.{methodName} should not use {unexpectedField.Name}.");
    }

    private static IEnumerable<MethodBase> GetImplementations(Type type, string methodName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var method in type.GetMethods(flags).Where(method => method.Name == methodName))
            yield return method;

        foreach (var nestedType in type.GetNestedTypes(BindingFlags.NonPublic))
        {
            if (!nestedType.Name.Contains($"<{methodName}>")) continue;

            var moveNext = nestedType.GetMethod("MoveNext", flags);
            if (moveNext != null)
                yield return moveNext;
        }
    }

    private static bool UsesField(MethodBase method, FieldInfo field)
    {
        var body = method.GetMethodBody()?.GetILAsByteArray();
        if (body is null) return false;

        var token = BitConverter.GetBytes(field.MetadataToken);
        for (var i = 0; i <= body.Length - token.Length; i++)
        {
            if (body[i] == token[0]
                && body[i + 1] == token[1]
                && body[i + 2] == token[2]
                && body[i + 3] == token[3])
                return true;
        }

        return false;
    }
}
