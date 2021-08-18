using System;

namespace Neo.ConsoleService
{
    public static class ConsoleHelper
    {
        private static readonly ConsoleColorSet InfoColor = new(ConsoleColor.Cyan);
        private static readonly ConsoleColorSet WarningColor = new(ConsoleColor.Yellow);
        private static readonly ConsoleColorSet ErrorColor = new(ConsoleColor.Red);

        public static void Info(params string[] values)
        {
            var currentColor = new ConsoleColorSet();

            for (int i = 0; i < values.Length; i++)
            {
                if (i % 2 == 0)
                    InfoColor.Apply();
                else
                    currentColor.Apply();
                Console.Write(values[i]);
            }
            currentColor.Apply();
            Console.WriteLine();
        }

        public static void Warning(string msg)
        {
            Log("Warning", WarningColor, msg);
        }

        public static void Error(string msg)
        {
            Log("Error", ErrorColor, msg);
        }

        private static void Log(string tag, ConsoleColorSet colorSet, string msg)
        {
            var currentColor = new ConsoleColorSet();

            colorSet.Apply();
            Console.Write($"{tag}: ");
            currentColor.Apply();
            Console.WriteLine(msg);
        }
    }
}
