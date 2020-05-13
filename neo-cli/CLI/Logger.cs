using Neo.Plugins;
using System;
using System.Text;
using System.IO;
using static System.IO.Path;
using System.Reflection;
using Neo.ConsoleService;

namespace Neo.CLI
{
    public class Logger : Plugin, ILogPlugin
    {
        public override string Name => "SystemLog";
        public override string ConfigFile => Combine(GetDirectoryName(Assembly.GetEntryAssembly().Location), "config.json");

        public bool Started { get; set; }

        public Logger() : base()
        {
            Started = Settings.Default.Logger.Started; // default is started to log
        }

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
            if (!Started)
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
                        case LogLevel.Debug: ConsoleColorSet.Debug.Apply(); break;
                        case LogLevel.Error: ConsoleColorSet.Error.Apply(); break;
                        case LogLevel.Fatal: ConsoleColorSet.Fatal.Apply(); break;
                        case LogLevel.Info: ConsoleColorSet.Info.Apply(); break;
                        case LogLevel.Warning: ConsoleColorSet.Warning.Apply(); break;
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
