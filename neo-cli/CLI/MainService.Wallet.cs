using Akka.Actor;
using Neo.ConsoleService;
using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.CLI
{
    partial class MainService
    {
        /// <summary>
        /// Process "open wallet" command
        /// </summary>
        /// <param name="path">Path</param>
        [ConsoleCommand("open wallet", Category = "Wallet Commands")]
        private void OnOpenWallet(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"File does not exist");
                return;
            }
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            try
            {
                OpenWallet(path, password);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                Console.WriteLine($"Failed to open file \"{path}\"");
            }
        }

        /// <summary>
        /// Process "close wallet" command
        /// </summary>
        [ConsoleCommand("close wallet", Category = "Wallet Commands")]
        private void OnCloseWalletCommand()
        {
            if (CurrentWallet == null)
            {
                Console.WriteLine($"Wallet is not opened");
                return;
            }
            CurrentWallet = null;
            Console.WriteLine($"Wallet is closed");
        }

        /// <summary>
        /// Process "upgrade wallet" command
        /// </summary>
        [ConsoleCommand("upgrade wallet", Category = "Wallet Commands")]
        private void OnUpgradeWalletCommand(string path)
        {
            if (Path.GetExtension(path).ToLowerInvariant() != ".db3")
            {
                Console.WriteLine("Can't upgrade the wallet file.");
                return;
            }
            if (!File.Exists(path))
            {
                Console.WriteLine("File does not exist.");
                return;
            }
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            string path_new = Path.ChangeExtension(path, ".json");
            if (File.Exists(path_new))
            {
                Console.WriteLine($"File '{path_new}' already exists");
                return;
            }
            NEP6Wallet.Migrate(path_new, path, password).Save();
            Console.WriteLine($"Wallet file upgrade complete. New wallet file has been auto-saved at: {path_new}");
        }

        /// <summary>
        /// Process "create address" command
        /// </summary>
        /// <param name="count">Count</param>
        [ConsoleCommand("create address", Category = "Wallet Commands")]
        private void OnCreateAddressCommand(ushort count = 1)
        {
            if (NoWallet()) return;

            string path = "address.txt";
            if (File.Exists(path))
            {
                if (!ReadUserInput($"The file '{path}' already exists, do you want to overwrite it? (yes|no)", false).IsYes())
                {
                    return;
                }
            }

            List<string> addresses = new List<string>();
            using (var percent = new ConsolePercent(0, count))
            {
                Parallel.For(0, count, (i) =>
                {
                    WalletAccount account = CurrentWallet.CreateAccount();
                    lock (addresses)
                    {
                        addresses.Add(account.Address);
                        percent.Value++;
                    }
                });
            }

            if (CurrentWallet is NEP6Wallet wallet)
                wallet.Save();

            Console.WriteLine($"Export addresses to {path}");
            File.WriteAllLines(path, addresses);
        }

        /// <summary>
        /// Process "export key" command
        /// </summary>
        /// <param name="path">Path</param>
        /// <param name="scriptHash">ScriptHash</param>
        [ConsoleCommand("export key", Category = "Wallet Commands")]
        private void OnExportKeyCommand(string path = null, UInt160 scriptHash = null)
        {
            if (NoWallet()) return;
            if (path != null && File.Exists(path))
            {
                Console.WriteLine($"Error: File '{path}' already exists");
                return;
            }
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            if (!CurrentWallet.VerifyPassword(password))
            {
                Console.WriteLine("Incorrect password");
                return;
            }
            IEnumerable<KeyPair> keys;
            if (scriptHash == null)
                keys = CurrentWallet.GetAccounts().Where(p => p.HasKey).Select(p => p.GetKey());
            else
            {
                var account = CurrentWallet.GetAccount(scriptHash);
                keys = account?.HasKey != true ? Array.Empty<KeyPair>() : new[] { account.GetKey() };
            }
            if (path == null)
                foreach (KeyPair key in keys)
                    Console.WriteLine(key.Export());
            else
                File.WriteAllLines(path, keys.Select(p => p.Export()));
        }

        /// <summary>
        /// Process "create wallet" command
        /// </summary>
        [ConsoleCommand("create wallet", Category = "Wallet Commands")]
        private void OnCreateWalletCommand(string path)
        {
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            string password2 = ReadUserInput("password", true);
            if (password != password2)
            {
                Console.WriteLine("Error");
                return;
            }
            if (!File.Exists(path))
            {
                CreateWallet(path, password);
            }
            else
            {
                Console.WriteLine("This wallet already exists, please create another one.");
            }
        }

        /// <summary>
        /// Process "import multisigaddress" command
        /// </summary>
        /// <param name="m">Required signatures</param>
        /// <param name="publicKeys">Public keys</param>
        [ConsoleCommand("import multisigaddress", Category = "Wallet Commands")]
        private void OnImportMultisigAddress(ushort m, ECPoint[] publicKeys)
        {
            if (NoWallet()) return;

            int n = publicKeys.Length;

            if (m < 1 || m > n || n > 1024)
            {
                Console.WriteLine("Error. Invalid parameters.");
                return;
            }

            Contract multiSignContract = Contract.CreateMultiSigContract(m, publicKeys);
            KeyPair keyPair = CurrentWallet.GetAccounts().FirstOrDefault(p => p.HasKey && publicKeys.Contains(p.GetKey().PublicKey))?.GetKey();

            WalletAccount account = CurrentWallet.CreateAccount(multiSignContract, keyPair);
            if (CurrentWallet is NEP6Wallet wallet)
                wallet.Save();

            Console.WriteLine("Multisig. Addr.: " + multiSignContract.Address);
        }

        /// <summary>
        /// Process "import key" command
        /// </summary>
        [ConsoleCommand("import key", Category = "Wallet Commands")]
        private void OnImportKeyCommand(string wifOrFile)
        {
            byte[] prikey = null;
            try
            {
                prikey = Wallet.GetPrivateKeyFromWIF(wifOrFile);
            }
            catch (FormatException) { }
            if (prikey == null)
            {
                var fileInfo = new FileInfo(wifOrFile);

                if (!fileInfo.Exists)
                {
                    Console.WriteLine($"Error: File '{fileInfo.FullName}' doesn't exists");
                    return;
                }

                if (wifOrFile.Length > 1024 * 1024)
                {
                    if (!ReadUserInput($"The file '{fileInfo.FullName}' is too big, do you want to continue? (yes|no)", false).IsYes())
                    {
                        return;
                    }
                }

                string[] lines = File.ReadAllLines(fileInfo.FullName).Where(u => !string.IsNullOrEmpty(u)).ToArray();
                using (var percent = new ConsolePercent(0, lines.Length))
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Length == 64)
                            prikey = lines[i].HexToBytes();
                        else
                            prikey = Wallet.GetPrivateKeyFromWIF(lines[i]);
                        CurrentWallet.CreateAccount(prikey);
                        Array.Clear(prikey, 0, prikey.Length);
                        percent.Value++;
                    }
                }
            }
            else
            {
                WalletAccount account = CurrentWallet.CreateAccount(prikey);
                Array.Clear(prikey, 0, prikey.Length);
                Console.WriteLine($"Address: {account.Address}");
                Console.WriteLine($" Pubkey: {account.GetKey().PublicKey.EncodePoint(true).ToHexString()}");
            }
            if (CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
        }

        /// <summary>
        /// Process "import watchonly" command
        /// </summary>
        [ConsoleCommand("import watchonly", Category = "Wallet Commands")]
        private void OnImportWatchOnlyCommand(string addressOrFile)
        {
            UInt160 address = null;
            try
            {
                address = StringToAddress(addressOrFile);
            }
            catch (FormatException) { }
            if (address is null)
            {
                var fileInfo = new FileInfo(addressOrFile);

                if (!fileInfo.Exists)
                {
                    Console.WriteLine($"Error: File '{fileInfo.FullName}' doesn't exists");
                    return;
                }

                if (fileInfo.Length > 1024 * 1024)
                {
                    if (!ReadUserInput($"The file '{fileInfo.FullName}' is too big, do you want to continue? (yes|no)", false).IsYes())
                    {
                        return;
                    }
                }

                string[] lines = File.ReadAllLines(fileInfo.FullName).Where(u => !string.IsNullOrEmpty(u)).ToArray();
                using (var percent = new ConsolePercent(0, lines.Length))
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        address = StringToAddress(lines[i]);
                        CurrentWallet.CreateAccount(address);
                        percent.Value++;
                    }
                }
            }
            else
            {
                WalletAccount account = CurrentWallet.CreateAccount(address);
                Console.WriteLine($"Address: {account.Address}");
            }
            if (CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
        }

        /// <summary>
        /// Process "list address" command
        /// </summary>
        [ConsoleCommand("list address", Category = "Wallet Commands")]
        private void OnListAddressCommand()
        {
            if (NoWallet()) return;

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                foreach (var account in CurrentWallet.GetAccounts())
                {
                    var contract = account.Contract;
                    var type = "Nonstandard";

                    if (account.WatchOnly)
                    {
                        type = "WatchOnly";
                    }
                    else if (contract.Script.IsMultiSigContract())
                    {
                        type = "MultiSignature";
                    }
                    else if (contract.Script.IsSignatureContract())
                    {
                        type = "Standard";
                    }
                    else if (snapshot.Contracts.TryGet(account.ScriptHash) != null)
                    {
                        type = "Deployed-Nonstandard";
                    }

                    Console.WriteLine($"{"   Address: "}{account.Address}\t{type}");
                    Console.WriteLine($"{"ScriptHash: "}{account.ScriptHash}\n");
                }
            }
        }

        /// <summary>
        /// Process "list asset" command
        /// </summary>
        [ConsoleCommand("list asset", Category = "Wallet Commands")]
        private void OnListAssetCommand()
        {
            if (NoWallet()) return;
            foreach (UInt160 account in CurrentWallet.GetAccounts().Select(p => p.ScriptHash))
            {
                Console.WriteLine(account.ToAddress());
                Console.WriteLine($"NEO: {CurrentWallet.GetBalance(NativeContract.NEO.Hash, account)}");
                Console.WriteLine($"GAS: {CurrentWallet.GetBalance(NativeContract.GAS.Hash, account)}");
                Console.WriteLine();
            }
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine("Total:   " + "NEO: " + CurrentWallet.GetAvailable(NativeContract.NEO.Hash) + "    GAS: " + CurrentWallet.GetAvailable(NativeContract.GAS.Hash));
            Console.WriteLine();
            Console.WriteLine("NEO hash: " + NativeContract.NEO.Hash);
            Console.WriteLine("GAS hash: " + NativeContract.GAS.Hash);
        }

        /// <summary>
        /// Process "list key" command
        /// </summary>
        [ConsoleCommand("list key", Category = "Wallet Commands")]
        private void OnListKeyCommand()
        {
            if (NoWallet()) return;
            foreach (WalletAccount account in CurrentWallet.GetAccounts().Where(p => p.HasKey))
            {
                Console.WriteLine($"   Address: {account.Address}");
                Console.WriteLine($"ScriptHash: {account.ScriptHash}");
                Console.WriteLine($" PublicKey: {account.GetKey().PublicKey.EncodePoint(true).ToHexString()}\n");
            }
        }

        /// <summary>
        /// Process "sign" command
        /// </summary>
        /// <param name="jsonObjectToSign">Json object to sign</param>
        [ConsoleCommand("sign", Category = "Wallet Commands")]
        private void OnSignCommand(JObject jsonObjectToSign)
        {
            if (NoWallet()) return;

            if (jsonObjectToSign == null)
            {
                Console.WriteLine("You must input JSON object pending signature data.");
                return;
            }
            try
            {
                ContractParametersContext context = ContractParametersContext.Parse(jsonObjectToSign.ToString());
                if (!CurrentWallet.Sign(context))
                {
                    Console.WriteLine("The private key that can sign the data is not found.");
                    return;
                }
                Console.WriteLine($"Signed Output:{Environment.NewLine}{context}");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
            }
        }

        /// <summary>
        /// Process "send" command
        /// </summary>
        /// <param name="asset">Asset id</param>
        /// <param name="to">To</param>
        /// <param name="amount">Amount</param>
        /// <param name="from">From</param>
        /// <param name="signerAccounts">Signer's accounts</param>
        [ConsoleCommand("send", Category = "Wallet Commands")]
        private void OnSendCommand(UInt160 asset, UInt160 to, string amount, UInt160 from = null, UInt160[] signerAccounts = null)
        {
            if (NoWallet()) return;
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            if (!CurrentWallet.VerifyPassword(password))
            {
                Console.WriteLine("Incorrect password");
                return;
            }

            Transaction tx;
            AssetDescriptor descriptor = new AssetDescriptor(asset);
            if (!BigDecimal.TryParse(amount, descriptor.Decimals, out BigDecimal decimalAmount) || decimalAmount.Sign <= 0)
            {
                Console.WriteLine("Incorrect Amount Format");
                return;
            }
            try
            {
                tx = CurrentWallet.MakeTransaction(new[]
                {
                    new TransferOutput
                    {
                        AssetId = asset,
                        Value = decimalAmount,
                        ScriptHash = to
                    }
                }, from: from, cosigners: signerAccounts?.Select(p => new Signer
                {
                    // default access for transfers should be valid only for first invocation
                    Scopes = WitnessScope.CalledByEntry,
                    Account = p
                })
                .ToArray() ?? new Signer[0]);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + GetExceptionMessage(e));
                return;
            }

            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return;
            }

            ContractParametersContext context = new ContractParametersContext(tx);
            CurrentWallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                NeoSystem.Blockchain.Tell(tx);
                Console.WriteLine($"TXID: {tx.Hash}");
            }
            else
            {
                Console.WriteLine("SignatureContext:");
                Console.WriteLine(context.ToString());
            }
        }

        /// <summary>
        /// Process "show gas" command
        /// </summary>
        [ConsoleCommand("show gas", Category = "Wallet Commands")]
        private void OnShowGasCommand()
        {
            if (NoWallet()) return;
            BigInteger gas = BigInteger.Zero;
            using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                foreach (UInt160 account in CurrentWallet.GetAccounts().Select(p => p.ScriptHash))
                {
                    gas += NativeContract.NEO.UnclaimedGas(snapshot, account, snapshot.Height + 1);
                }
            Console.WriteLine($"Unclaimed gas: {new BigDecimal(gas, NativeContract.GAS.Decimals)}");
        }

        /// <summary>
        /// Process "change password" command
        /// </summary>
        [ConsoleCommand("change password", Category = "Wallet Commands")]
        private void OnChangePasswordCommand()
        {
            if (NoWallet()) return;
            string oldPassword = ReadUserInput("password", true);
            if (oldPassword.Length == 0)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            if (!CurrentWallet.VerifyPassword(oldPassword))
            {
                Console.WriteLine("Incorrect password");
                return;
            }
            string newPassword = ReadUserInput("New password", true);
            string newPasswordReEntered = ReadUserInput("Re-Enter Password", true);
            if (!newPassword.Equals(newPasswordReEntered))
            {
                Console.WriteLine("Two passwords entered are inconsistent!");
                return;
            }

            if (CurrentWallet is NEP6Wallet wallet)
            {
                string backupFile = wallet.Path + ".bak";
                if (!File.Exists(wallet.Path) || File.Exists(backupFile))
                {
                    Console.WriteLine("Wallet backup fail");
                    return;
                }
                try
                {
                    File.Copy(wallet.Path, backupFile);
                }
                catch (IOException)
                {
                    Console.WriteLine("Wallet backup fail");
                    return;
                }
            }

            bool succeed = CurrentWallet.ChangePassword(oldPassword, newPassword);
            if (succeed)
            {
                if (CurrentWallet is NEP6Wallet nep6Wallet)
                    nep6Wallet.Save();
                Console.WriteLine("Password changed successfully");
            }
            else
            {
                Console.WriteLine("Failed to change password");
            }
        }

        private void SignAndSendTx(Transaction tx)
        {
            ContractParametersContext context;
            try
            {
                context = new ContractParametersContext(tx);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine($"Error creating contract params: " + GetExceptionMessage(e));
                throw;
            }
            CurrentWallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                NeoSystem.Blockchain.Tell(tx);
                Console.WriteLine($"Signed and relayed transaction with hash={tx.Hash}");
                return;
            }
            Console.WriteLine($"Failed sending transaction with hash={tx.Hash}");
        }
    }
}
