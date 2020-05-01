using Neo.Plugins;
using System;
using System.IO;

namespace Neo
{
    class Logger : Plugin, ILogPlugin
    {
        static object _lock = new object();

        static Logger()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            lock (_lock)
            {
                using FileStream fs = new FileStream("error.log", FileMode.Create, FileAccess.Write, FileShare.None);
                using StreamWriter w = new StreamWriter(fs);

                if (e.ExceptionObject is Exception ex)
                {
                    PrintErrorLogs(w, ex);
                }
                else
                {
                    w.WriteLine($"{DateTime.UtcNow.ToString()} [ERROR:{e.ExceptionObject.GetType()}] ");
                    w.WriteLine(e.ExceptionObject);
                }
            }
        }

        private static void PrintErrorLogs(StreamWriter writer, Exception ex)
        {
            writer.WriteLine($"{DateTime.UtcNow.ToString()} [ERROR:{ex.GetType()}] ");
            writer.WriteLine(ex.Message);
            writer.WriteLine(ex.StackTrace);
            if (ex is AggregateException ex2)
            {
                foreach (Exception inner in ex2.InnerExceptions)
                {
                    writer.WriteLine();
                    PrintErrorLogs(writer, inner);
                }
            }
            else if (ex.InnerException != null)
            {
                writer.WriteLine();
                PrintErrorLogs(writer, ex.InnerException);
            }
        }

        public void Log(string source, LogLevel level, string message)
        {
            if (level < LogLevel.Error) return;

            lock (_lock)
            {
                using FileStream fs = new FileStream("error.log", FileMode.Create, FileAccess.Write, FileShare.None);
                using StreamWriter w = new StreamWriter(fs);

                w.WriteLine($"{DateTime.UtcNow.ToString()} [{level}:{source}] {message}");
            }
        }
    }
}
