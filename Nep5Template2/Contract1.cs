using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.ComponentModel;
using System.Numerics;

namespace NEP5
{
    [Features(ContractFeatures.HasStorage | ContractFeatures.Payable)]
    public class NEP5 : SmartContract
    {
        [DisplayName("Transfer")]   
        public static event Action<byte[], byte[], BigInteger> Transferred;
        private static readonly byte[] Owner = "NWySkpUYVB15zUHmdwVZsQFk8MpHgn1PUJ".ToScriptHash(); //Owner Address
        //private static readonly byte[] Owner2 = "NiPdgAkdYQM63Q1KZnXWiH5HmnFQcWLpzN".ToScriptHash(); //Owner2 Address
        private static readonly BigInteger TotalSupplyValue = 10000000000000000;


        public static object Main(string method, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(Owner);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //var callscript = ExecutionEngine.CallingScriptHash;

                if (method == "balanceOf") return BalanceOf((byte[])args[0]);

                if (method == "decimals") return Decimals();

                if (method == "deploy") return Deploy();

                if (method == "name") return Name();

                if (method == "symbol") return Symbol();

                if (method == "supportedStandards") return SupportedStandards();

                if (method == "totalSupply") return TotalSupply();

                if (method == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
            }
            return false;
        }

        [DisplayName("BalanceOf")]
        public static BigInteger BalanceOf(byte[] account)
        {
            if (account.Length != 20)
                throw new InvalidOperationException("The parameter account SHOULD be 20-byte addresses.");
            var balance = Storage.Get(Storage.CurrentContext, account);
            byte[] byteZero = new byte[] { 0 };
            if (balance is null)
            {
                Storage.Put(Storage.CurrentContext, account, byteZero);
            }
            return Storage.Get(Storage.CurrentContext, account).AsBigInteger();
        }
        [DisplayName("Decimals")]
        public static byte Decimals() => 8;

        private static bool IsPayable(byte[] to)
        {
            var c = Blockchain.GetContract(to);
            return c == null || c.IsPayable;
        }

        [DisplayName("Deploy")]
        public static bool Deploy()
        {
            //Runtime.CheckWitness need PR536(https://github.com/neo-project/neo-node/pull/536) or PR545(https://github.com/neo-project/neo-node/pull/545)
            if (!Runtime.CheckWitness(Owner)) return false;
            //if (!Runtime.CheckWitness(Owner2)) return false;
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            Storage.Put(Storage.CurrentContext, Owner, TotalSupplyValue);
            Storage.Put(Storage.CurrentContext, "totalSupply", TotalSupplyValue);
            Transferred(null, Owner, TotalSupplyValue);
            return true;
        }

        [DisplayName("Name")]
        public static string Name() => "Nep5 Neo3 Template Owen 6"; //name of the token

        [DisplayName("Symbol")]
        public static string Symbol() => "NND"; //symbol of the token

        [DisplayName("SupportedStandards")]
        public static string[] SupportedStandards() => new string[] { "NEP-5", "NEP-7", "NEP-10" };

        [DisplayName("TotalSupply")]
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }
        /*#if DEBUG
                [DisplayName("transfer")] //Only for ABI file
                public static bool Transfer(byte[] from, byte[] to, BigInteger amount) => true;
        #endif*/
        //Methods of actual execution
        private static bool Transfer(byte[] from, byte[] to, BigInteger amount)
        {
            if (amount <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            if (to.Length != 20) return false;

            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < amount) return false;
            if (from == to) return true;
            if (from_value == amount)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - amount);
            var to_value = Storage.Get(Storage.CurrentContext, to);
            if (to_value != null)
            {
                Runtime.Log("if pass");
                Storage.Put(Storage.CurrentContext, to, to_value.AsBigInteger() + amount);
                Runtime.Log("Storage.put pass");
            }
            else
            {
                Runtime.Log("else pass");
                Storage.Put(Storage.CurrentContext, to, amount);
                Runtime.Log("amount -> pass");
            }
            //var to_value = Storage.Get(Storage.CurrentContext, to)?.AsBigInteger() ?? 0;
            //Storage.Put(Storage.CurrentContext, to, to_value + amount);
            Transferred(from, to, amount);
            return true;
        }
    }
}
