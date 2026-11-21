// Copyright (C) 2015-2026 The Neo Project.
//
// UT_MinimumRequiredFeeReader.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RpcServer.Tests;

[TestClass]
public class UT_MinimumRequiredFeeReader
{
    private static long? Read<T>(T engine, long consumed)
        => new MinimumRequiredFeeReader<T>().Read(engine, consumed);

    [TestMethod]
    public void MissingPropertyOmitsCapability()
        => Assert.IsNull(Read(new LegacyEngine(), 100));

    [TestMethod]
    public void PresentPropertyPreservesExactValue()
    {
        Assert.AreEqual(200L, Read(new CapableEngine(200), 100));
        Assert.AreEqual(0L, Read(new CapableEngine(0), 0));
        Assert.AreEqual(long.MaxValue, Read(new CapableEngine(long.MaxValue), 1));
    }

    [TestMethod]
    public void CachedGetterReadsFreshValuesWithoutPerReadAllocation()
    {
        var reader = new MinimumRequiredFeeReader<MutableEngine>();
        var engine = new MutableEngine { MinimumRequiredFee = 100 };
        Assert.AreEqual(100L, reader.Read(engine, 100));
        engine.MinimumRequiredFee = 200;
        Assert.AreEqual(200L, reader.Read(engine, 100));
        for (int i = 0; i < 10_000; i++) reader.Read(engine, 100);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++) reader.Read(engine, 100);
        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [TestMethod]
    public void BelowConsumedAndNegativeValuesFailClosed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Read(new CapableEngine(99), 100));
        Assert.ThrowsExactly<InvalidOperationException>(() => Read(new CapableEngine(-1), 0));
    }

    [TestMethod]
    public void ThrowingGetterDoesNotDegradeToLegacyFee()
        => Assert.ThrowsExactly<NotSupportedException>(() => Read(new ThrowingEngine(), 100));

    [TestMethod]
    public void MalformedPropertiesFailClosed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Read(new WrongTypeEngine(), 100));
        Assert.ThrowsExactly<InvalidOperationException>(() => Read(new StaticEngine(), 100));
        Assert.ThrowsExactly<InvalidOperationException>(() => Read(new PrivateGetterEngine(), 100));
        Assert.ThrowsExactly<InvalidOperationException>(() => Read(new IndexedEngine(), 100));
    }

    public class LegacyEngine;
    public class MutableEngine { public long MinimumRequiredFee { get; set; } }
    public class CapableEngine(long value) { public long MinimumRequiredFee => value; }
    public class ThrowingEngine { public long MinimumRequiredFee => throw new NotSupportedException("Getter failed."); }
    public class WrongTypeEngine { public int MinimumRequiredFee => 200; }
    public class StaticEngine { public static long MinimumRequiredFee => 200; }
    public class PrivateGetterEngine { public long MinimumRequiredFee { private get; set; } }
    public class IndexedEngine
    {
        [System.Runtime.CompilerServices.IndexerName("MinimumRequiredFee")]
        public long this[int index] => 200;
    }
}
