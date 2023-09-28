// Copyright (C) 2016-2023 The Neo Project.
//
// The neo-cli is free software distributed under the MIT software
// license, see the accompanying file LICENSE in the main directory of
// the project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.


using System;
using System.Linq;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;
using Neo.VM;
using Neo.ScriptHelper;

#if DEBUG
NeoSystem NeoSystem = new(null);
#endif

// Create an instance of ScriptHelper
ScriptHelper helper = new ScriptHelper(NeoSystem);

// loading key pair
var keyPair = new KeyPair(Wallet.GetPrivateKeyFromWIF("xxxxxxxxxxxxxxxxxxxxxxxx-your private key"));
var scriptHash = Contract.CreateSignatureContract(keyPair.PublicKey).ScriptHash;
Console.WriteLine($"ME: {scriptHash} {scriptHash.ToAddress(NeoSystem.Settings.AddressVersion)}");

// get snapshot
var snapshot = NeoSystem.GetSnapshot();
// get latest block index
var blockIndex = NativeContract.Ledger.CurrentIndex(snapshot);
// get latest block
var block = NativeContract.Ledger.GetBlock(snapshot, blockIndex);

// constructing transaction
var msg = @"XXX";
var sb = new ScriptBuilder();
sb.EmitPush(msg);
var transaction = new Transaction
{
    Version = 0,
    Nonce = (uint)new Random().NextInt64(),
    SystemFee = 1000000,
    NetworkFee = 0x3e500,
    Attributes = new TransactionAttribute[],
    Script = sb.ToArray(),
    Signers = new Signer[] { new Signer { Account = scriptHash, Scopes = WitnessScope.CalledByEntry } },
    ValidUntilBlock = blockIndex + 1,
};

// sign the transaction
var signature = transaction.Sign(keyPair, NeoSystem.Settings.Network);
var invocationScript = new byte[] { ((byte)OpCode.PUSHDATA1), 64 }.Concat(signature).ToArray();
var verificationScript = Contract.CreateSignatureContract(keyPair.PublicKey).Script;
transaction.Witnesses = new Witness[] { new Witness { InvocationScript = invocationScript, VerificationScript = verificationScript } };
Console.WriteLine($"HASH: {transaction.Hash}");
Console.WriteLine($"{transaction.Verify(NeoSystem.Settings, snapshot, new(), new Transaction[] { })}");


var txRes = await helper.SendRawTransactionAsync(transaction);



// // get validators
// var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, NeoSystem.Settings.ValidatorsCount);
// // get next primary
// UInt160 primary = Contract.CreateSignatureRedeemScript(validators[(block.PrimaryIndex + 1) % NeoSystem.Settings.ValidatorsCount]).ToScriptHash();
// Console.WriteLine($"NEXT SPEAKER: {primary.ToAddress(NeoSystem.Settings.AddressVersion)}");
//
// Console.WriteLine($"{NativeContract.NEO.BalanceOf(NeoSystem.GetSnapshot(), UInt160.Zero)}");
