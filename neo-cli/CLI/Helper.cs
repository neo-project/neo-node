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
