using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Neo.GUI
{
    internal partial class ConsoleForm : Form
    {
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
            Thread thread = new Thread(Program.Service.RunConsole);
            thread.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            queue.Enqueue("exit\n");
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            base.OnFormClosing(e);
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string line = $"{textBox2.Text}{Environment.NewLine}";
                textBox1.AppendText(line);
                if (textBox2.Text == "clear")
                    textBox1.Clear();
                queue.Enqueue(line);
                textBox2.Clear();
            }
        }
    }
}
