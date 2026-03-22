// Copyright (C) 2015-2026 The Neo Project.
//
// BulkPayDialog.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract.Native;
using Neo.Wallets;
using static Neo.Program;

namespace Neo.GUI;

internal partial class BulkPayDialog : Form
{
    public BulkPayDialog(TokenState asset = null)
    {
        InitializeComponent();
        if (asset == null)
        {
            var snapshot = Service.NeoSystem.StoreView;
            foreach (UInt160 assetId in NEP5Watched)
            {
                try
                {
                    var descriptor = NativeContract.TokenManagement.GetTokenInfo(snapshot, assetId);
                    comboBox1.Items.Add(new KeyValuePair<UInt160, TokenState>(assetId, descriptor));
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }
        }
        else
        {
            var tokenId = TokenManagement.GetAssetId(asset.Owner, asset.Name);
            comboBox1.Items.Add(new KeyValuePair<UInt160, TokenState>(tokenId, asset));
            comboBox1.SelectedIndex = 0;
            comboBox1.Enabled = false;
        }
    }

    public TxOutListBoxItem[] GetOutputs()
    {
        var asset = (KeyValuePair<UInt160, TokenState>)comboBox1.SelectedItem;
        return textBox1.Lines.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p =>
        {
            string[] line = p.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return new TxOutListBoxItem
            {
                AssetName = asset.Value.Name,
                AssetId = asset.Key,
                Value = BigDecimal.Parse(line[1], asset.Value.Decimals),
                ScriptHash = line[0].ToScriptHash(Service.NeoSystem.Settings.AddressVersion)
            };
        }).Where(p => p.Value.Value != 0).ToArray();
    }

    private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (comboBox1.SelectedItem is KeyValuePair<UInt160, TokenState> asset)
        {
            textBox3.Text = Service.CurrentWallet.GetAvailable(Service.NeoSystem.StoreView, asset.Key).ToString();
        }
        else
        {
            textBox3.Text = "";
        }
        textBox1_TextChanged(this, EventArgs.Empty);
    }

    private void textBox1_TextChanged(object sender, EventArgs e)
    {
        button1.Enabled = comboBox1.SelectedIndex >= 0 && textBox1.TextLength > 0;
    }
}
