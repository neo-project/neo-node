using System.Diagnostics;
using System.Threading;

namespace neo_cli_restart
{
    class Program
    {
        static void Main(string[] args)
        {
            // Thread.Sleep(500);

            var psi = new ProcessStartInfo(@"dotnet")
            {
                Arguments = @"neo-cli.dll",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }
    }
}
