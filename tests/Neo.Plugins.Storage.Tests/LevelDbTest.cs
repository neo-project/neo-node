// Copyright (C) 2015-2026 The Neo Project.
//
// LevelDbTest.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System.Reflection;

namespace Neo.Plugins.Storage.Tests;

[TestClass]
public class LevelDbTest
{
    [TestMethod]
    public void TestLevelDbDatabase()
    {
        using var db = DB.Open(Path.GetRandomFileName(), new() { CreateIfMissing = true });

        db.Put(WriteOptions.Default, [0x00, 0x00, 0x01], [0x01]);
        db.Put(WriteOptions.Default, [0x00, 0x00, 0x02], [0x02]);
        db.Put(WriteOptions.Default, [0x00, 0x00, 0x03], [0x03]);

        CollectionAssert.AreEqual(new byte[] { 0x01, }, db.Get(ReadOptions.Default, [0x00, 0x00, 0x01]));
        CollectionAssert.AreEqual(new byte[] { 0x02, }, db.Get(ReadOptions.Default, [0x00, 0x00, 0x02]));
        CollectionAssert.AreEqual(new byte[] { 0x03, }, db.Get(ReadOptions.Default, [0x00, 0x00, 0x03]));
    }

    [TestMethod]
    public void TestLevelDbSnapshotDisposeDisposesWriteBatch()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        IStore? store = null;
        IStoreSnapshot? snapshot = null;

        try
        {
            var provider = StoreFactory.GetStoreProvider("LevelDBStore");
            Assert.IsNotNull(provider);

            store = provider.GetStore(path);
            snapshot = store.GetSnapshot();

            var batchField = snapshot.GetType().GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(batchField);

            var batch = batchField.GetValue(snapshot) as WriteBatch;
            Assert.IsNotNull(batch);
            Assert.IsFalse(batch.IsDisposed);

            snapshot.Dispose();
            snapshot = null;

            Assert.IsTrue(batch.IsDisposed);
        }
        finally
        {
            snapshot?.Dispose();
            store?.Dispose();

            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
