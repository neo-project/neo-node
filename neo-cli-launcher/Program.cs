using System.Diagnostics;
using System.Threading;

namespace neo_cli_launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            Process.Start(new ProcessStartInfo(@"dotnet")
            {
                Arguments = @"neo-cli.dll",
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }
    }
}
