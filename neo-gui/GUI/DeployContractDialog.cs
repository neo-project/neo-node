using Neo.SmartContract;
using Neo.VM;
using System;
using System.IO;
using System.Windows.Forms;

namespace Neo.GUI
{
    internal partial class DeployContractDialog : Form
    {
        public DeployContractDialog()
        {
            InitializeComponent();
        }

        public byte[] GetScript()
        {
            byte[] script = textBox8.Text.HexToBytes();
            string manifest = "";
            using ScriptBuilder sb = new ScriptBuilder();
            sb.EmitSysCall(ApplicationEngine.System_Contract_Create, script, manifest);
            return sb.ToArray();
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            button2.Enabled = textBox1.TextLength > 0
                && textBox2.TextLength > 0
                && textBox3.TextLength > 0
                && textBox4.TextLength > 0
                && textBox5.TextLength > 0
                && textBox8.TextLength > 0;
            try
            {
                textBox9.Text = textBox8.Text.HexToBytes().ToScriptHash().ToString();
            }
            catch (FormatException)
            {
                textBox9.Text = "";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            textBox8.Text = File.ReadAllBytes(openFileDialog1.FileName).ToHexString();
        }
    }
}
