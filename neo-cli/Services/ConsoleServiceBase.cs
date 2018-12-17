using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security;
using System.Text;

namespace Neo.Services
{
    public abstract class ConsoleServiceBase
    {
        protected virtual string Prompt => "service";

        public abstract string ServiceName { get; }

        protected bool ShowPrompt { get; set; } = true;

        protected virtual bool OnCommand(string[] args)
        {
            switch (args[0].ToLower())
            {
                case "clear":
                    Console.Clear();
                    return true;
                case "exit":
                    return false;
                case "version":
                    Console.WriteLine(Assembly.GetEntryAssembly().GetName().Version);
                    return true;
                default:
                    Console.WriteLine("error: command not found " + args[0]);
                    return true;
            }
        }

        protected internal abstract void OnStart(string[] args);

        protected internal abstract void OnStop();

        public static string ReadPassword(string prompt)
        {
            const string t = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            StringBuilder sb = new StringBuilder();
            ConsoleKeyInfo key;
            Console.Write(prompt);
            Console.Write(": ");

            Console.ForegroundColor = ConsoleColor.Yellow;

            do
            {
                key = Console.ReadKey(true);
                if (t.IndexOf(key.KeyChar) != -1)
                {
                    sb.Append(key.KeyChar);
                    Console.Write('*');
                }
                else if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
                {
                    sb.Length--;
                    Console.Write(key.KeyChar);
                    Console.Write(' ');
                    Console.Write(key.KeyChar);
                }
            } while (key.Key != ConsoleKey.Enter);

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            return sb.ToString();
        }

        public static SecureString ReadSecureString(string prompt)
        {
            const string t = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            SecureString securePwd = new SecureString();
            ConsoleKeyInfo key;
            Console.Write(prompt);
            Console.Write(": ");

            Console.ForegroundColor = ConsoleColor.Yellow;

            do
            {
                key = Console.ReadKey(true);
                if (t.IndexOf(key.KeyChar) != -1)
                {
                    securePwd.AppendChar(key.KeyChar);
                    Console.Write('*');
                }
                else if (key.Key == ConsoleKey.Backspace && securePwd.Length > 0)
                {
                    securePwd.RemoveAt(securePwd.Length - 1);
                    Console.Write(key.KeyChar);
                    Console.Write(' ');
                    Console.Write(key.KeyChar);
                }
            } while (key.Key != ConsoleKey.Enter);

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            securePwd.MakeReadOnly();
            return securePwd;
        }

        public void Run(string[] args)
        {
            OnStart(args);
            RunConsole();
            OnStop();
        }

        private void RunConsole()
        {
            bool running = true;
#if NET461
            Console.Title = ServiceName;
#endif
            Console.OutputEncoding = Encoding.Unicode;

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Version ver = Assembly.GetEntryAssembly().GetName().Version;
            Console.WriteLine($"{ServiceName} Version: {ver}");
            Console.WriteLine();

            // Simple handling of quoted or non quoted arguments.
            string[] SplitArgStrings(string line)
            {
                bool inQuote = false;
                List<string> outputArgs = new List<String>();
                StringBuilder builder = new StringBuilder();

                void AddArgFromBuilder()
                {
                    string arg = builder.ToString();
                    if (arg.Length > 0)
                    {
                        outputArgs.Add(arg);
                        builder.Clear();
                    }
                }

                for (int index = 0; index < line.Length; index++)
                {
                    char c = line[index];
                    if (c == '\\')
                    {
                        if (++index >= line.Length)
                        {
                            break;
                        }

                        builder.Append(line[index]);
                        continue;
                    }

                    if (!inQuote && c == ' ')
                    {
                        AddArgFromBuilder();
                        continue;
                    }
                    if (c == '"')
                    {
                        inQuote = !inQuote;
                        continue;
                    }

                    builder.Append(c);
                }
                AddArgFromBuilder();

                return outputArgs.ToArray();
            }

            while (running)
            {
                if (ShowPrompt)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{Prompt}> ");
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                string line = Console.ReadLine()?.Trim();
                if (line == null) break;
                Console.ForegroundColor = ConsoleColor.White;

                string[] args = SplitArgStrings(line);
                if (args.Length == 0)
                    continue;
                try
                {
                    running = OnCommand(args);
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"error: {ex.Message}");
#else
                    Console.WriteLine("error");
#endif
                }
            }

            Console.ResetColor();
        }
    }
}
