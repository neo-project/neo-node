using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.CLI.CommandParser;
using System.Linq;

namespace neo_cli.Tests
{
    [TestClass]
    public class CommandTokenTest
    {
        [TestMethod]
        public void Test1()
        {
            var cmd = " ";
            var args = CommandToken.Parse(cmd).ToArray();

            AreEqual(args, new CommandSpaceToken(0, 1));
            Assert.AreEqual(cmd, CommandToken.ToString(args));
        }

        [TestMethod]
        public void Test2()
        {
            var cmd = "show  state";
            var args = CommandToken.Parse(cmd).ToArray();

            AreEqual(args, new CommandStringToken(0, "show"), new CommandSpaceToken(4, 2), new CommandStringToken(6, "state"));
            Assert.AreEqual(cmd, CommandToken.ToString(args));
        }

        [TestMethod]
        public void Test3()
        {
            var cmd = "show \"hello world\"";
            var args = CommandToken.Parse(cmd).ToArray();

            AreEqual(args, new CommandStringToken(0, "show"), new CommandSpaceToken(4, 1), new CommandStringToken(5, "hello world", true));
            Assert.AreEqual(cmd, CommandToken.ToString(args));
        }

        private void AreEqual(CommandToken[] args, params CommandToken[] compare)
        {
            Assert.AreEqual(compare.Length, args.Length);

            for (int x = 0; x < args.Length; x++)
            {
                var a = args[x];
                var b = compare[x];

                Assert.AreEqual(a.Type, b.Type);
                Assert.AreEqual(a.Value, b.Value);
                Assert.AreEqual(a.Offset, b.Offset);
            }
        }
    }
}
