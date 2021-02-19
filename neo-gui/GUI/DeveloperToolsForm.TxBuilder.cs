using Neo.GUI.Wrappers;
using Neo.SmartContract;
using System;

namespace Neo.GUI
{
    partial class DeveloperToolsForm
    {
        private void InitializeTxBuilder()
        {
            propertyGrid1.SelectedObject = new TransactionWrapper();
        }

        private void propertyGrid1_SelectedObjectsChanged(object sender, EventArgs e)
        {
            splitContainer1.Panel2.Enabled = propertyGrid1.SelectedObject != null;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            TransactionWrapper wrapper = (TransactionWrapper)propertyGrid1.SelectedObject;
            ContractParametersContext context = new ContractParametersContext(Program.Service.NeoSystem.StoreView, wrapper.Unwrap());
            InformationBox.Show(context.ToString(), "ParametersContext", "ParametersContext");
        }
    }
}
