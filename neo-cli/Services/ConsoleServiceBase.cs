using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.ServiceProcess;
using System.Text;

namespace Neo.Services
{
    public abstract class ConsoleServiceBase
    {
        protected virtual string Depends => null;
        protected virtual string Prompt => "service";

        public abstract string ServiceName { get; }

        protected bool ShowPrompt { get; set; } = true;

        protected virtual bool OnCommand(string[] args)
        {
            switch (args[0].ToLower())
            {
                case "":
                    return true;
                case "clear":
                    Console.Clear();
                    return true;
                case "exit":
                    return false;
                case "version":
                    Console.WriteLine(Assembly.GetEntryAssembly().GetVersion());
                    return true;
                default:
                    Console.WriteLine("error: command not found " + args[0]);
                    return true;
            }
        }

        protected internal abstract void OnStart(string[] args);

        protected internal abstract void OnStop();

        private static string[] ParseCommandLine(string line)
        {
            List<string> outputArgs = new List<string>();
            using (StringReader reader = new StringReader(line))
            {
                while (true)
                {
                    switch (reader.Peek())
                    {
                        case -1:
                            return outputArgs.ToArray();
                        case ' ':
                            reader.Read();
                            break;
                        case '\"':
                            outputArgs.Add(ParseCommandLineString(reader));
                            break;
                        default:
                            outputArgs.Add(ParseCommandLineArgument(reader));
                            break;
                    }
                }
            }
        }

        private static string ParseCommandLineArgument(TextReader reader)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                int c = reader.Read();
                switch (c)
                {
                    case -1:
                    case ' ':
                        return sb.ToString();
                    default:
                        sb.Append((char)c);
                        break;
                }
            }
        }

        private static string ParseCommandLineString(TextReader reader)
        {
            if (reader.Read() != '\"') throw new FormatException();
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                int c = reader.Peek();
                switch (c)
                {
                    case '\"':
                        reader.Read();
                        return sb.ToString();
                    case '\\':
                        sb.Append(ParseEscapeCharacter(reader));
                        break;
                    default:
                        reader.Read();
                        sb.Append((char)c);
                        break;
                }
            }
        }

        private static char ParseEscapeCharacter(TextReader reader)
        {
            if (reader.Read() != '\\') throw new FormatException();
            int c = reader.Read();
            switch (c)
            {
                case -1:
                    throw new FormatException();
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                case 'x':
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < 2; i++)
                    {
                        int h = reader.Read();
                        if (h >= '0' && h <= '9' || h >= 'A' && h <= 'F' || h >= 'a' && h <= 'f')
                            sb.Append((char)h);
                        else
                            throw new FormatException();
                    }
                    return (char)byte.Parse(sb.ToString(), NumberStyles.AllowHexSpecifier);
                default:
                    return (char)c;
            }
        }

        public static string ReadUserInput(string prompt, bool password = false)
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
                    if (password)
                    {
                        Console.Write('*');
                    }
                    else
                    {
                        Console.Write(key.KeyChar);
                    }
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
            if (Environment.UserInteractive)
            {
                if (args.Length > 0 && args[0] == "/install")
                {
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    {
                        Console.WriteLine("Only support for installing services on Windows.");
                        return;
                    }
                    string arguments = string.Format("create {0} start= auto binPath= \"{1}\"", ServiceName, Process.GetCurrentProcess().MainModule.FileName);
                    if (!string.IsNullOrEmpty(Depends))
                    {
                        arguments += string.Format(" depend= {0}", Depends);
                    }
                    Process process = Process.Start(new ProcessStartInfo
                    {
                        Arguments = arguments,
                        FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    });
                    process.WaitForExit();
                    Console.Write(process.StandardOutput.ReadToEnd());
                }
                else if (args.Length > 0 && args[0] == "/uninstall")
                {
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    {
                        Console.WriteLine("Only support for installing services on Windows.");
                        return;
                    }
                    Process process = Process.Start(new ProcessStartInfo
                    {
                        Arguments = string.Format("delete {0}", ServiceName),
                        FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    });
                    process.WaitForExit();
                    Console.Write(process.StandardOutput.ReadToEnd());
                }
                else
                {
                    OnStart(args);
                    RunConsole();
                    OnStop();
                }
            }
            else
            {
                ServiceBase.Run(new ServiceProxy(this));
            }
        }

        private void RunConsole()
        {
            bool running = true;
            string[] emptyarg = new string[] { "" };
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                Console.Title = ServiceName;

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"{ServiceName} Version: {Assembly.GetEntryAssembly().GetVersion()}");
            Console.WriteLine();

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

                try
                {
                    string[] args = ParseCommandLine(line);
                    if (args.Length == 0)
                        args = emptyarg;

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
