using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Neo.GUI
{
    internal class TextBoxWriter : TextWriter
    {
        private readonly TextBoxBase textBox;

        public override Encoding Encoding => Encoding.UTF8;

        public TextBoxWriter(TextBoxBase textBox)
        {
            this.textBox = textBox;
        }

        public override void Write(char value)
        {
            textBox.Invoke(new Action(() => { textBox.Text += value; }));
        }

        public override void Write(string value)
        {
            textBox.Invoke(new Action<string>(textBox.AppendText), value);
        }
    }
}
