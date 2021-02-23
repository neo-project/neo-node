using Neo.Wallets;

namespace Neo.GUI
{
    internal class TxOutListBoxItem : TransferOutput
    {
        public string AssetName;

        public override string ToString()
        {
            return $"{ScriptHash.ToAddress(Program.Service.NeoSystem.Settings.AddressVersion)}\t{Value}\t{AssetName}";
        }
    }
}
