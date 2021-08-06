using System.IO;
using System.Security.Cryptography;

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

        // Compute the file's hash.
        public static string GetHashSha256(string filename)
        {
            using ( SHA256 Sha256 = SHA256.Create())
            {
                using (FileStream stream = File.OpenRead(filename))
                {
                    return Sha256.ComputeHash(stream).ToHexString().ToString();
                }
            }
          
        }

    }
}
