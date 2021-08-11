using System;
using System.IO;
using System.Net;

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


        public static MemoryStream DownloadFile(HttpWebResponse response, string fileName)
        {
            var totalRead = 0L;
            byte[] buffer = new byte[1024];
            int read;

            using Stream stream = response.GetResponseStream();

            var output = new MemoryStream();
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                totalRead += read;
                Console.Write($"\rDownloading {fileName}.zip {totalRead / 1024}KB/{response.ContentLength / 1024}KB {(totalRead * 100) / response.ContentLength}%");
            }
            Console.WriteLine();

            return output;
        }

    }

}
