using Neo.CLI;
using System;

namespace Neo
{
    static class Program
    {
        static void Main(string[] args)
        {
            _ = new Logger();
            var mainService = new MainService();
            mainService.Run(args);
        }
    }
}
