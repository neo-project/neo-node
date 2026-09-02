// Copyright (C) 2015-2026 The Neo Project.
//
// UT_OracleService.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.OracleService.Protocols;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System.Collections.Concurrent;
using static Neo.Plugins.OracleService.Tests.TestBlockchain;

namespace Neo.Plugins.OracleService.Tests;

[TestClass]
public class UT_OracleService
{
    [TestMethod]
    public void TestFilter()
    {
        var json = """
        {
            "Stores": ["Lambton Quay",  "Willis Street"],
            "Manufacturers": [{
                "Name": "Acme Co",
                "Products": [{ "Name": "Anvil", "Price": 50 }]
            },{
                "Name": "Contoso",
                "Products": [
                    { "Name": "Elbow Grease", "Price": 99.95 },
                    { "Name": "Headlight Fluid", "Price": 4 }
                ]
            }]
        }
        """;
        Assert.AreEqual(@"[""Acme Co""]", OracleService.Filter(json, "$.Manufacturers[0].Name").ToStrictUtf8String());
        Assert.AreEqual("[50]", OracleService.Filter(json, "$.Manufacturers[0].Products[0].Price").ToStrictUtf8String());
        Assert.AreEqual(@"[""Elbow Grease""]",
            OracleService.Filter(json, "$.Manufacturers[1].Products[0].Name").ToStrictUtf8String());
        Assert.AreEqual(@"[{""Name"":""Elbow Grease"",""Price"":99.95}]",
            OracleService.Filter(json, "$.Manufacturers[1].Products[0]").ToStrictUtf8String());
    }

    [TestMethod]
    public void CollectRefreshSignatures_WatchOnlyDoesNotThrow()
    {
        var wallet = TestUtils.GenerateTestWallet("pwd");
        wallet.CreateAccount(UInt160.Zero);
        var signs = new Dictionary<ECPoint, byte[]>();

        var matches = OracleService.CollectRefreshSignatures(wallet, signs);
        Assert.IsEmpty(matches);
    }

    [TestMethod]
    public void CollectRefreshSignatures_WatchOnlyThenKeyedStillMatches()
    {
        var wallet = TestUtils.GenerateTestWallet("pwd");
        wallet.CreateAccount(UInt160.Parse("0x0000000000000000000000000000000000000001"));
        var keyed = wallet.CreateAccount();
        Assert.IsNotNull(keyed);
        var key = keyed.GetKey();
        Assert.IsNotNull(key);

        byte[] signature = [1, 2, 3];
        var signs = new Dictionary<ECPoint, byte[]> { [key.PublicKey] = signature };

        var matches = OracleService.CollectRefreshSignatures(wallet, signs);
        Assert.HasCount(1, matches);
        Assert.AreEqual(key.PublicKey, matches[0].Key.PublicKey);
        CollectionAssert.AreEqual(signature, matches[0].Sign);
    }

    [TestMethod]
    public void CollectRefreshSignatures_SkipsLockedAccount()
    {
        var wallet = TestUtils.GenerateTestWallet("pwd");
        var account = wallet.CreateAccount();
        Assert.IsNotNull(account);
        var key = account.GetKey();
        Assert.IsNotNull(key);
        account.Lock = true;

        var signs = new Dictionary<ECPoint, byte[]> { [key.PublicKey] = new byte[] { 9 } };
        var matches = OracleService.CollectRefreshSignatures(wallet, signs);
        Assert.IsEmpty(matches);
    }

    [TestMethod]
    public void CollectRefreshSignatures_IgnoresUnrelatedKeys()
    {
        var wallet = TestUtils.GenerateTestWallet("pwd");
        wallet.CreateAccount();
        var other = TestUtils.GenerateTestWallet("pwd").CreateAccount()!.GetKey()!;
        var signs = new Dictionary<ECPoint, byte[]> { [other.PublicKey] = new byte[] { 4 } };

        var matches = OracleService.CollectRefreshSignatures(wallet, signs);
        Assert.IsEmpty(matches);
    }

    [TestMethod]
    public void TestCreateOracleResponseTx()
    {
        var snapshotCache = TestBlockchain.GetTestSnapshotCache();
        var index = NativeContract.Ledger.CurrentIndex(snapshotCache);
        var executionFactor = NativeContract.Policy.GetExecFeeFactor(TestUtils.settings, snapshotCache, index);
        Assert.AreEqual(30, executionFactor);

        var feePerByte = NativeContract.Policy.GetFeePerByte(snapshotCache);
        Assert.AreEqual(1000, feePerByte);

        OracleRequest request = new()
        {
            OriginalTxid = UInt256.Zero,
            GasForResponse = 100000000 * 1,
            Url = "https://127.0.0.1/test",
            Filter = "",
            CallbackContract = UInt160.Zero,
            CallbackMethod = "callback",
            UserData = []
        };

        byte Prefix_Transaction = 11;
        snapshotCache.Add(NativeContract.Ledger.CreateStorageKey(Prefix_Transaction, request.OriginalTxid), new(new TransactionState()
        {
            BlockIndex = 1,
            Transaction = new()
            {
                Signers = [],
                Attributes = [],
                ValidUntilBlock = 1,
                Witnesses = []
            }
        }));

        OracleResponse response = new() { Id = 1, Code = OracleResponseCode.Success, Result = new byte[] { 0x00 } };
        ECPoint[] oracleNodes = [ECCurve.Secp256r1.G];
        var tx = OracleService.CreateResponseTx(snapshotCache, request, response, oracleNodes, TestUtils.settings);

        Assert.AreEqual(166, tx.Size);
        Assert.AreEqual(2198650, tx.NetworkFee);
        Assert.AreEqual(97801350, tx.SystemFee);

        // case (2) The size of attribute exceed the maximum limit

        request.GasForResponse = 0_10000000;
        response.Result = new byte[10250];
        tx = OracleService.CreateResponseTx(snapshotCache, request, response, oracleNodes, TestUtils.settings);
        Assert.AreEqual(165, tx.Size);
        Assert.AreEqual(OracleResponseCode.InsufficientFunds, response.Code);
        Assert.AreEqual(2197650, tx.NetworkFee);
        Assert.AreEqual(7802350, tx.SystemFee);
    }

    [TestMethod]
    public void TestMarkRequestFinishedUsesCurrentTimestamp()
    {
        var oracle = new OracleService();

        var before = TimeProvider.Current.UtcNow;
        oracle.MarkRequestFinished(1ul);
        var after = TimeProvider.Current.UtcNow;

        var finishedCache = oracle.finishedCache;
        Assert.IsTrue(finishedCache.TryGetValue(1ul, out var timestamp));
        Assert.IsTrue(timestamp >= before && timestamp <= after);
    }

    [TestMethod]
    public async Task TestStartAfterStopUsesFreshCancellationSource()
    {
        ResetStore();
        InitializeContract();
        DesignateOracleRole();

        var oracle = new OracleService
        {
            _system = s_theNeoSystem
        };
        Task firstRun = Task.CompletedTask;
        Task secondRun = Task.CompletedTask;

        try
        {
            firstRun = oracle.Start(s_wallet);
            await Task.Delay(100);
            StopOracle(oracle);
            await firstRun.WaitAsync(TimeSpan.FromSeconds(5));

            secondRun = oracle.Start(s_wallet);
            await Task.Delay(100);

            Assert.IsFalse(oracle.cancelSource.IsCancellationRequested);
        }
        finally
        {
            StopOracle(oracle);
            await secondRun.WaitAsync(TimeSpan.FromSeconds(5));
            oracle.Dispose();
        }
    }

    [TestMethod]
    public void TestStartWhileStoppingDoesNotResetCurrentRun()
    {
        var oracle = new OracleService();

        var currentRun = Task.CompletedTask;
        oracle.status = OracleService.OracleStatus.Stopping;
        oracle.processingTask = currentRun;
        var cancellationSource = oracle.cancelSource;

        Task returnedRun = oracle.Start(null!);

        Assert.AreSame(currentRun, returnedRun);
        Assert.AreSame(cancellationSource, oracle.cancelSource);
        oracle.status = OracleService.OracleStatus.Stopped;
        oracle.Dispose();
    }

    [TestMethod]
    public async Task TestStartDisposesStaleProtocols()
    {
        ResetStore();
        InitializeContract();
        DesignateOracleRole();

        var oracle = new OracleService();
        var protocol = new TrackingProtocol();
        oracle._system = s_theNeoSystem;
        oracle.protocols["stale"] = protocol;

        Task run = Task.CompletedTask;
        try
        {
            run = oracle.Start(s_wallet);
            await Task.Delay(100);

            Assert.IsTrue(protocol.Disposed);
        }
        finally
        {
            StopOracle(oracle);
            await run.WaitAsync(TimeSpan.FromSeconds(5));
            oracle.Dispose();
        }
    }

    private static void StopOracle(OracleService oracle)
    {
        oracle.OnStop();
    }

    private sealed class TrackingProtocol : IOracleProtocol
    {
        public bool Disposed { get; private set; }

        public void Configure()
        {
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public Task<(OracleResponseCode, string)> ProcessAsync(Uri uri, CancellationToken cancellation)
        {
            return Task.FromResult((OracleResponseCode.Success, string.Empty));
        }
    }
}
