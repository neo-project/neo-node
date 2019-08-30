using Neo.SmartContract.Native.Tokens;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.Cli.Extensions
{
    class KeyValueParser
    {

        public static string DefaultKeyParser(object value)
        {
            var byteValue = (byte[])value;
            var prefix = byteValue.First();
            //Prefix
            var keyValue = byteValue.Skip(1);
            var outputKey = keyValue.ToHexString();
            if(outputKey.Length == 40)
            {
                var scriptHash = UInt160.Parse(outputKey);
                outputKey = scriptHash.ToAddress();
            }

            var result = $"{prefix} / {outputKey}";
            return result;
        }

        public static string DefaultValueParser(object value)
        {
            var byteValue = (byte[])value;
            var state = new Nep5AccountState();
            state.FromByteArray(byteValue);
            return state.Balance.ToString();
        }

    }
}
