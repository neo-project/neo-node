using Neo.IO.Json;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;
using Boolean = Neo.VM.Types.Boolean;
using Buffer = Neo.VM.Types.Buffer;

namespace Neo.CLI
{
    internal static class Helper
    {
        public static bool ToBool(this string input)
        {
            if (input == null) return false;

            input = input.ToLowerInvariant();

            return input == "true" || input == "yes" || input == "1";
        }

        public static bool IsYes(this string input)
        {
            if (input == null) return false;

            input = input.ToLowerInvariant();

            return input == "yes" || input == "y";
        }
    }
}
