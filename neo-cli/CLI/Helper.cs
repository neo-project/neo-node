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
    }
}
