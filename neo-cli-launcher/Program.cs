using System.Diagnostics;
using System.Threading;

namespace neo_cli_launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.Sleep(2000);
            Process.Start(new ProcessStartInfo(@"dotnet")
            {
                Arguments = @"neo-cli.dll",
                UseShellExecute = false,
            });
        }
    }
}
