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

using Neo.Persistence;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Neo.Plugins.Storage.Tests;

[TestClass]
public class SnapshotReadOptionsTest
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);

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
        AssertLevelDbFillCache(snapshotType, scanReadOptions, false);
        AssertLevelDbFillCache(snapshotType, pointReadOptions, true);
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

#pragma warning disable CS0618 // Exercise both supported TryGet overloads.
    [TestMethod]
    [DataRow("LevelDBStore")]
    [DataRow("RocksDBStore")]
    public void PointAndScanReadsKeepTheSameSnapshot(string providerName)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var provider = StoreFactory.GetStoreProvider(providerName);
            Assert.IsNotNull(provider);
            using var store = provider.GetStore(path);
            store.Put([1], [10]);
            store.Put([2], []);
            using var snapshot = store.GetSnapshot();

            if (providerName == "RocksDBStore")
            {
                AssertRocksDbFillCache(snapshot, "_scanOptions", false);
                AssertRocksDbFillCache(snapshot, "_pointOptions", true);
            }

            store.Put([1], [20]);
            store.Delete([2]);
            store.Put([3], [30]);

            AssertSnapshotValue(snapshot, [1], [10]);
            AssertSnapshotValue(snapshot, [2], []);
            Assert.IsFalse(snapshot.Contains([3]));
            Assert.IsNull(snapshot.TryGet([3]));
            Assert.IsFalse(snapshot.TryGet([3], out var missing));
            Assert.IsNull(missing);

            var forward = snapshot.Find(null, SeekDirection.Forward).ToArray();
            Assert.HasCount(2, forward);
            CollectionAssert.AreEqual(new byte[] { 1 }, forward[0].Key);
            CollectionAssert.AreEqual(new byte[] { 10 }, forward[0].Value);
            CollectionAssert.AreEqual(new byte[] { 2 }, forward[1].Key);
            Assert.IsEmpty(forward[1].Value);
            var backward = snapshot.Find([2], SeekDirection.Backward).ToArray();
            Assert.HasCount(2, backward);
            CollectionAssert.AreEqual(forward[1].Key, backward[0].Key);
            CollectionAssert.AreEqual(forward[1].Value, backward[0].Value);
            CollectionAssert.AreEqual(forward[0].Key, backward[1].Key);
            CollectionAssert.AreEqual(forward[0].Value, backward[1].Value);

            CollectionAssert.AreEqual(new byte[] { 20 }, store.TryGet([1]));
            Assert.IsFalse(store.Contains([2]));
            Assert.IsTrue(store.Contains([3]));
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    private static void AssertSnapshotValue(IStoreSnapshot snapshot, byte[] key, byte[] expected)
    {
        Assert.IsTrue(snapshot.Contains(key));
        CollectionAssert.AreEqual(expected, snapshot.TryGet(key));
        Assert.IsTrue(snapshot.TryGet(key, out var value));
        CollectionAssert.AreEqual(expected, value);
    }
#pragma warning restore CS0618

    private static void AssertRocksDbFillCache(IStoreSnapshot snapshot, string fieldName, bool expected)
    {
        var options = (RocksDbSharp.ReadOptions)GetField(snapshot.GetType(), fieldName).GetValue(snapshot)!;
        Assert.AreEqual(expected ? (byte)1 : (byte)0,
            RocksDbSharp.Native.Instance.rocksdb_readoptions_get_fill_cache(options.Handle), fieldName);
        GC.KeepAlive(options);
    }

    private static void AssertLevelDbFillCache(Type type, FieldInfo field, bool expected)
    {
        // LevelDB exposes no native getter. Inspect the exact initializer assigned
        // to this field, including the setter's boolean argument.
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var instructions = ReadInstructions(constructor).ToArray();
        int storeIndex = Array.FindIndex(instructions, instruction =>
            instruction.Code == OpCodes.Stfld && instruction.Token == field.MetadataToken);
        Assert.IsTrue(storeIndex >= 0, $"Missing initialization for {field.Name}.");
        int createIndex = Array.FindLastIndex(instructions, storeIndex, instruction => instruction.Code == OpCodes.Newobj);
        Assert.IsTrue(createIndex >= 0);
        var setter = typeof(Neo.IO.Data.LevelDB.ReadOptions).GetProperty("FillCache")!.SetMethod;
        var calls = Enumerable.Range(createIndex + 1, storeIndex - createIndex - 1)
            .Where(index => (instructions[index].Code == OpCodes.Call || instructions[index].Code == OpCodes.Callvirt)
                && constructor.Module.ResolveMethod(instructions[index].Token) == setter).ToArray();
        Assert.HasCount(1, calls, $"{field.Name} must explicitly configure FillCache.");
        Assert.AreEqual(expected ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0,
            instructions[calls[0] - 1].Code, $"Incorrect FillCache for {field.Name}.");
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
        foreach (var implementation in implementations)
        {
            var instructions = ReadInstructions(implementation).ToArray();
            Assert.IsTrue(instructions.Any(instruction => instruction.Code == OpCodes.Ldfld
                && instruction.Token == expectedField.MetadataToken),
                $"{implementation} should load {expectedField.Name}.");
            Assert.IsFalse(instructions.Any(instruction =>
                (instruction.Code == OpCodes.Ldfld || instruction.Code == OpCodes.Ldflda)
                && instruction.Token == unexpectedField.MetadataToken),
                $"{implementation} should not load {unexpectedField.Name}.");
        }
    }

    private static IEnumerable<MethodBase> GetImplementations(Type type, string methodName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var method in type.GetMethods(flags).Where(method => method.Name == methodName))
        {
            var iterator = method.GetCustomAttribute<IteratorStateMachineAttribute>();
            yield return iterator is null ? method : iterator.StateMachineType.GetMethod("MoveNext", flags)!;
        }
    }

    private static IEnumerable<(OpCode Code, int Token)> ReadInstructions(MethodBase method)
    {
        var body = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(body);
        using var reader = new BinaryReader(new MemoryStream(body));
        while (reader.BaseStream.Position < body.Length)
        {
            byte first = reader.ReadByte();
            short value = first == 0xfe ? unchecked((short)(0xfe00 | reader.ReadByte())) : first;
            var code = OpCodesByValue[value];
            int token = 0;
            if (code.OperandType is OperandType.InlineField or OperandType.InlineMethod)
                token = reader.ReadInt32();
            else
            {
                int size = code.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineSig
                        or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
                        or OperandType.ShortInlineR => 4,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => reader.ReadInt32() * 4,
                    _ => throw new InvalidOperationException($"Unexpected IL operand: {code.OperandType}")
                };
                reader.BaseStream.Position += size;
            }
            yield return (code, token);
        }
    }
}
