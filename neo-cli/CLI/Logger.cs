using Neo.ConsoleService;
using Neo.Plugins;
using System;
using System.IO;
using System.Text;
using static System.IO.Path;

namespace Neo.CLI
{
    internal class Logger : Plugin, ILogPlugin
    {
        private static readonly ConsoleColorSet DebugColor = new ConsoleColorSet(ConsoleColor.Cyan);
        private static readonly ConsoleColorSet InfoColor = new ConsoleColorSet(ConsoleColor.White);
        private static readonly ConsoleColorSet WarningColor = new ConsoleColorSet(ConsoleColor.Yellow);
        private static readonly ConsoleColorSet ErrorColor = new ConsoleColorSet(ConsoleColor.Red);
        private static readonly ConsoleColorSet FatalColor = new ConsoleColorSet(ConsoleColor.Red);

        public override string Name => "SystemLog";
        public override string Description => "Prints consensus log and is a built-in plugin which cannot be uninstalled";
        public override string ConfigFile => Combine(GetDirectoryName(Path), "config.json");
        public override string Path => GetType().Assembly.Location;

        private static void GetErrorLogs(StringBuilder sb, Exception ex)
        {
            sb.AppendLine(ex.GetType().ToString());
            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.StackTrace);
            if (ex is AggregateException ex2)
            {
                foreach (Exception inner in ex2.InnerExceptions)
                {
                    sb.AppendLine();
                    GetErrorLogs(sb, inner);
                }
            }
            else if (ex.InnerException != null)
            {
                sb.AppendLine();
                GetErrorLogs(sb, ex.InnerException);
            }
        }

        void ILogPlugin.Log(string source, LogLevel level, object message)
        {
            if (!Settings.Default.Logger.Active)
                return;

            if (message is Exception ex)
            {
                var sb = new StringBuilder();
                GetErrorLogs(sb, ex);
                message = sb.ToString();
            }

            lock (typeof(Logger))
            {
                DateTime now = DateTime.Now;
                var log = $"[{now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}";

                if (Settings.Default.Logger.ConsoleOutput)
                {
                    var currentColor = new ConsoleColorSet();

                    switch (level)
                    {
                        case LogLevel.Debug: DebugColor.Apply(); break;
                        case LogLevel.Error: ErrorColor.Apply(); break;
                        case LogLevel.Fatal: FatalColor.Apply(); break;
                        case LogLevel.Info: InfoColor.Apply(); break;
                        case LogLevel.Warning: WarningColor.Apply(); break;
                    }

                    Console.WriteLine(log);
                    currentColor.Apply();
                }

                if (!string.IsNullOrEmpty(Settings.Default.Logger.Path))
                {
                    StringBuilder sb = new StringBuilder(source);
                    foreach (char c in GetInvalidFileNameChars())
                        sb.Replace(c, '-');
                    var path = Combine(Settings.Default.Logger.Path, sb.ToString());
                    Directory.CreateDirectory(path);
                    path = Combine(path, $"{now:yyyy-MM-dd}.log");
                    try
                    {
                        File.AppendAllLines(path, new[] { $"[{level}]{log}" });
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Error writing the log file: " + path);
                    }
                }
            }
        }
    }
}
