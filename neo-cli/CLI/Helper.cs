// Copyright (C) 2016-2021 The Neo Project.
// 
// The neo-cli is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.CLI
{
    internal static class Helper
    {
        public static bool IsYes(this string input)
        {
            if (input == null) return false;

            input = input.ToLowerInvariant();

            return input == "yes" || input == "y";
        }

        public static bool DoubleCheckPwd(string password, string password2)
        {
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return false;
            }
            if (password != password2)
            {
                Console.WriteLine("Two passwords are inconsistent.");
                return false;
            }
            return true;
        }

        public static string ToBase64String(this byte[] input) => System.Convert.ToBase64String(input);
    }
}
