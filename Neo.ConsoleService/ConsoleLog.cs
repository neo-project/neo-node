using System;

namespace Neo.ConsoleService
{
    public class ConsoleLog
    {
        public static void WriteLine(string msg)
        {
            Console.WriteLine(msg);
        }

        public static void Info(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                if (i % 2 == 1)
                    Console.ForegroundColor = ConsoleColor.White;
                Console.Write(values[i]);
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
        }

        public static void Warning(string msg)
        {
            Log("Warning", ConsoleColor.Yellow, msg);
        }

        public static void Error(string msg)
        {
            Log("Error", ConsoleColor.Red, msg);
        }

        private static void Log(string tag, ConsoleColor tagColor,  string msg, ConsoleColor msgColor = ConsoleColor.White)
        {
            Console.ForegroundColor = tagColor;
            Console.Write($"{tag}: ");
            Console.ForegroundColor = msgColor;
            Console.WriteLine(msg);
        }


    }
}
