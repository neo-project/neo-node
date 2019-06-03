using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Plugins;
using Neo.Network.P2P.Payloads;
using Neo;
using Neo.Persistence;
using Settings = Neo.Plugins.Settings;
using System.Collections.Generic;
using Neo.Cryptography;
using System.Numerics;
using System.Collections;
using System.Linq;
using System;
using Moq;
// neo-cli
using Neo.Shell;

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
        public void TestOnInvoke()
        {
            string[] args = {"invoke", "0x0000000000000000000000000000000000000000"};
            uut.ParseParametersInvoke(args, out UInt160 scriptHash, out ContractParameter[] parameters));

            scriptHash.Should().Be(UInt160.Zero);
        }
    }
}
