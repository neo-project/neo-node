using Neo.CLI;
using Neo.GUI;
using System;
using System.Windows.Forms;

namespace Neo
{
    static class Program
    {
        internal static MainService Service = new MainService();

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Service.Start(args);
            Application.Run(new ConsoleForm());
            Service.Stop();
        }
    }
}
