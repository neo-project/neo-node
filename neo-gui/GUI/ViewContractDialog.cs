using Neo.SmartContract;
using System.Linq;
using System.Windows.Forms;
using Neo.Wallets;

namespace Neo.GUI
{
    public partial class ViewContractDialog : Form
    {
        public ViewContractDialog(Contract contract)
        {
            InitializeComponent();
            textBox1.Text = contract.ScriptHash.ToAddress(Program.Service.NeoSystem.Settings.AddressVersion);
            textBox2.Text = contract.ScriptHash.ToString();
            textBox3.Text = contract.ParameterList.Cast<byte>().ToArray().ToHexString();
            textBox4.Text = contract.Script.ToHexString();
        }
    }
}
