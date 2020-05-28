using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Neo.GUI
{
    internal partial class ConsoleForm : Form
    {
        private Thread thread;
        private readonly QueueReader queue = new QueueReader();

        public ConsoleForm()
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Console.SetOut(new TextBoxWriter(textBox1));
            Console.SetIn(queue);
            thread = new Thread(Program.Service.RunConsole);
            thread.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            queue.Enqueue($"exit{Environment.NewLine}");
            thread.Join();
            Console.SetIn(new StreamReader(Console.OpenStandardInput()));
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            base.OnFormClosing(e);
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string line = $"{textBox2.Text}{Environment.NewLine}";
                textBox1.AppendText(Program.Service.ReadingPassword ? "***" : line);
                switch (textBox2.Text.ToLower())
                {
                    case "clear":
                        textBox1.Clear();
                        break;
                    case "exit":
                        Close();
                        return;
                }
                queue.Enqueue(line);
                textBox2.Clear();
            }
        }
    }
}
