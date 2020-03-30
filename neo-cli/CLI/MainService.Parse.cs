using Neo.ConsoleService;
using Neo.IO;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "parse" command
        /// </summary>
        [ConsoleCommand("parse", Category = "Base Commands", Description = "Parse a value to its possible conversions.")]
        private void OnParseCommand(string value)
        {
            var input = value;
            var parseFunctions = new Dictionary<string, Func<string, string>>()
            {
                { "Address to ScriptHash", AddressToScripthash },
                { "Address to Base64", AddressToBase64 },
                { "ScriptHash to Address", ScripthashToAddress },
                { "Base64 to Address", Base64ToAddress },
                { "Base64 to String", Base64ToStr },
                { "Base64 to Big Integer", Base64ToNumber },
                { "Big Integer to Hex String", NumberToHex },
                { "Big Integer to Base64", NumberToBase64 },
                { "Hex String to String", HexToString },
                { "Hex String to Big Integer", HexToNumber },
                { "String to Hex String", StringToHex },
                { "String to Base64", StringToBase64 }
            };

            bool any = false;

            foreach (var pair in parseFunctions)
            {
                try
                {
                    var parseMethod = pair.Value;
                    var result = parseMethod(input);

                    Console.WriteLine($"{pair.Key,-30}\t{result}");
                    any = true;
                }
                catch (ArgumentException)
                {
                    // couldn't parse the value
                }
            }

            if (!any)
            {
                Console.WriteLine($"Was not possible to convert: '{input}'");
            }
        }

        /// <summary>
        /// Converts an hexadecimal value to an UTF-8 string
        /// </summary>
        /// <param name="hexString">
        /// Hexadecimal value to be converted
        /// </param>
        /// <returns>
        /// The string represented by the hexadecimal value
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when is not possible to parse the hexadecimal value to a UTF-8 string.
        /// </exception>
        private string HexToString(string hexString)
        {
            try
            {
                var clearHexString = ClearHexString(hexString);
                var bytes = clearHexString.HexToBytes();
                var utf8String = Encoding.UTF8.GetString(bytes);

                return utf8String;
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
            catch (DecoderFallbackException)
            {
                throw new ArgumentException();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts an hex value to a big integer
        /// </summary>
        /// <param name="hexString">
        /// Hexadecimal value to be converted
        /// </param>
        /// <returns>
        /// The string that represents the converted big integer
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when is not possible to parse the hex value to big integer value.
        /// </exception>
        private string HexToNumber(string hexString)
        {
            try
            {
                var clearHexString = ClearHexString(hexString);
                var bytes = clearHexString.HexToBytes();
                var number = new BigInteger(bytes);

                return number.ToString();
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Formats a string value to a default hexadecimal representation of a byte array
        /// </summary>
        /// <param name="hexString">
        /// The string value to be formatted
        /// </param>
        /// <returns>
        /// Returns the formatted string.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when is the string is not a valid hex representation of a byte array.
        /// </exception>
        private string ClearHexString(string hexString)
        {
            bool hasHexPrefix = hexString.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase);

            try
            {
                if (hasHexPrefix)
                {
                    hexString = hexString.Substring(2);
                }

                if (hexString.Length % 2 == 1)
                {
                    // if the length is an odd number, it cannot be parsed to a byte array
                    // it may be a valid hex string, so include a leading zero to parse correctly
                    hexString = "0" + hexString;
                }

                if (hasHexPrefix)
                {
                    // if the input value starts with '0x', the first byte is the less significant
                    // to parse correctly, reverse the byte array
                    return hexString.HexToBytes().Reverse().ToArray().ToHexString();
                }
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }

            return hexString;
        }

        /// <summary>
        /// Converts a string in a hexadecimal value
        /// </summary>
        /// <param name="strParam">
        /// String value to be converted
        /// </param>
        /// <returns>
        /// The hexadecimal value that represents the converted string
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when is not possible to parse the string value to a hexadecimal value.
        /// </exception>
        private string StringToHex(string strParam)
        {
            try
            {
                var bytesParam = Encoding.UTF8.GetBytes(strParam);
                return bytesParam.ToHexString();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
            catch (EncoderFallbackException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts a string in Base64 string
        /// </summary>
        /// <param name="strParam">
        /// String value to be converted
        /// </param>
        /// <returns>
        /// The Base64 value that represents the converted string
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when is not possible to parse the string value to a Base64 value.
        /// </exception>
        private string StringToBase64(string strParam)
        {
            try
            {
                byte[] bytearray = Encoding.ASCII.GetBytes(strParam);
                string base64 = Convert.ToBase64String(bytearray.AsSpan());
                return base64;
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
            catch (EncoderFallbackException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts a string number in hexadecimal format
        /// </summary>
        /// <param name="strParam">
        /// String that represents the number to be converted
        /// </param>
        /// <returns>
        /// The string that represents the converted hexadecimal value
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent a big integer value or when
        /// it is not possible to parse the big integer value to hexadecimal.
        /// </exception>
        private string NumberToHex(string strParam)
        {
            try
            {
                if (!BigInteger.TryParse(strParam, out var numberParam))
                {
                    throw new ArgumentException();
                }
                return numberParam.ToByteArray().ToHexString();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
            catch (EncoderFallbackException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Prints the desired number in Base64 byte array
        /// </summary>
        /// <param name="strParam">
        /// String that represents the number to be converted
        /// </param>
        /// <returns>
        /// The string that represents the converted Base64 value
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent a big integer value or when
        /// it is not possible to parse the big integer value to Base64 value.
        /// </exception>
        private string NumberToBase64(string strParam)
        {
            try
            {
                if (!BigInteger.TryParse(strParam, out var number))
                {
                    throw new ArgumentException();
                }
                byte[] bytearray = number.ToByteArray();
                string base64 = Convert.ToBase64String(bytearray.AsSpan());

                return base64;
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts an address to its corresponding scripthash
        /// </summary>
        /// <param name="address">
        /// String that represents the address to be converted
        /// </param>
        /// <returns>
        /// The string that represents the converted scripthash
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent an address or when
        /// it is not possible to parse the address to scripthash.
        /// </exception>
        private string AddressToScripthash(string address)
        {
            try
            {
                var bigEndScript = address.ToScriptHash();

                return bigEndScript.ToString();
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts an address to Base64 byte array
        /// </summary>
        /// <param name="address">
        /// String that represents the address to be converted
        /// </param>
        /// <returns>
        /// The string that represents the converted Base64 value
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent an address or when
        /// it is not possible to parse the address to Base64 value.
        /// </exception>
        private string AddressToBase64(string address)
        {
            try
            {
                var script = address.ToScriptHash();
                string base64 = Convert.ToBase64String(script.ToArray().AsSpan());

                return base64;
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts a big end script hash to its equivalent address
        /// </summary>
        /// <param name="script">
        /// String that represents the scripthash to be converted
        /// </param>
        /// <returns>
        /// The string that represents the converted address
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent an scripthash.
        /// </exception>
        private string ScripthashToAddress(string script)
        {
            try
            {
                UInt160 scriptHash;
                if (script.StartsWith("0x"))
                {
                    if (!UInt160.TryParse(script, out scriptHash))
                    {
                        throw new ArgumentException();
                    }
                }
                else
                {
                    if (!UInt160.TryParse(script, out UInt160 littleEndScript))
                    {
                        throw new ArgumentException();
                    }
                    string bigEndScript = littleEndScript.ToArray().ToHexString();
                    if (!UInt160.TryParse(bigEndScript, out scriptHash))
                    {
                        throw new ArgumentException();
                    }
                }

                var hexScript = scriptHash.ToAddress();
                return hexScript;
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts an Base64 byte array to address
        /// </summary>
        /// <param name="bytearray">
        /// String that represents the Base64 value
        /// </param>
        /// <returns>
        /// The string that represents the converted address
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent an Base64 value or when
        /// it is not possible to parse the Base64 value to address.
        private string Base64ToAddress(string bytearray)
        {
            try
            {
                byte[] result = Convert.FromBase64String(bytearray).Reverse().ToArray();
                string hex = result.ToHexString();

                if (!UInt160.TryParse(hex, out var scripthash))
                {
                    throw new ArgumentException();
                }

                string address = scripthash.ToAddress();
                return address;
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts an Base64 hex string to string
        /// </summary>
        /// <param name="bytearray">
        /// String that represents the Base64 value
        /// </param>
        /// <returns>
        /// The string that represents the converted string
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent an Base64 value or when
        /// it is not possible to parse the Base64 value to string value.
        private string Base64ToStr(string bytearray)
        {
            try
            {
                byte[] result = Convert.FromBase64String(bytearray);
                string str = Encoding.ASCII.GetString(result);
                return str;
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
            catch (DecoderFallbackException)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Converts an Base64 hex string to big integer value
        /// </summary>
        /// <param name="bytearray">
        /// String that represents the Base64 value
        /// </param>
        /// <returns>
        /// The string that represents the converted big integer
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Throw when the string does not represent an Base64 value or when
        /// it is not possible to parse the Base64 value to big integer value.
        private string Base64ToNumber(string bytearray)
        {
            try
            {
                var bytes = Convert.FromBase64String(bytearray);
                var number = new BigInteger(bytes);
                return number.ToString();
            }
            catch (FormatException)
            {
                throw new ArgumentException();
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException();
            }
        }
    }
}
