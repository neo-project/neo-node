using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.Network.P2P.Payloads;
using Neo;
using Neo.Persistence;
//using Settings = Neo.Plugins.Settings;
using System.Collections.Generic;
using Neo.Cryptography;
using System.Numerics;
using System.Collections;
using System.Linq;
using System;
using Moq;
// neo-cli
using Neo.Shell;
using Neo.SmartContract;

namespace NeoCli.UnitTests
{
    [TestClass]
    public class UT_MainService
    {
        private static readonly Random _random = new Random(11121990);

        MainService uut;

        [TestInitialize]
        public void TestSetup()
        {
            uut = new MainService();
        }

        [TestMethod]
        public void TestTemplate()
        {
            // Nothing to do here now... put some tests
        }
    }
}
