using Neo.CLI;

namespace Neo
{
    static class Program
    {
        static void Main(string[] args)
        {
            var mainService = new MainService();
            mainService.Run(args);
        }
    }
}
