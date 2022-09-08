// Copyright (C) 2016-2022 The Neo Project.
// 
// The neo-cli is free software distributed under the MIT software 
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.IO;

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

        public static string ToBase64String(this byte[] input) => System.Convert.ToBase64String(input);

        // get the actual case of a path with case-insensitive file system
        public static string GetActualPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var actualPath = Directory.GetCurrentDirectory();

            foreach (var dir in parts)
            {

                var dirs = Directory.GetDirectories(actualPath, dir);
                if (dirs.Length == 0)
                    return path;

                actualPath = Path.Combine(actualPath, dirs[0]);
            }

            return actualPath;
        }
    }
}
