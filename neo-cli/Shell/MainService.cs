using Neo.Consensus;
using Neo.Core;
using Neo.Implementations.Blockchains.LevelDB;
using Neo.Implementations.Wallets.EntityFramework;
using Neo.Implementations.Wallets.NEP6;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network;
using Neo.Network.Payloads;
using Neo.Network.RPC;
using Neo.Services;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Neo.Shell
{
    internal class MainService : ConsoleServiceBase
    {
        private const string PeerStatePath = "peers.dat";

        private RpcServerWithWallet rpc;
        private ConsensusWithPolicy consensus;

        protected LocalNode LocalNode { get; private set; }
        protected override string Prompt => "neo";
        public override string ServiceName => "NEO-CLI";

        private void ImportBlocks(Stream stream)
        {
            LevelDBBlockchain blockchain = (LevelDBBlockchain)Blockchain.Default;
            using (BinaryReader r = new BinaryReader(stream))
            {
                uint count = r.ReadUInt32();
                for (int height = 0; height < count; height++)
                {
                    byte[] array = r.ReadBytes(r.ReadInt32());
                    if (height > blockchain.Height)
                    {
                        Block block = array.AsSerializable<Block>();
                        blockchain.AddBlockDirectly(block);
                    }
                }
            }
        }

        protected override bool OnCommand(string[] args)
        {
            switch (args[0].ToLower())
            {
                case "broadcast":
                    return OnBroadcastCommand(args);
                case "create":
                    return OnCreateCommand(args);
                case "export":
                    return OnExportCommand(args);
                case "help":
                    return OnHelpCommand(args);
                case "import":
                    return OnImportCommand(args);
                case "list":
                    return OnListCommand(args);
                case "claim":
                    return OnClaimCommand(args);
                case "open":
                    return OnOpenCommand(args);
                case "rebuild":
                    return OnRebuildCommand(args);
                case "refresh":
                    return OnRefreshCommand(args);
                case "send":
                    return OnSendCommand(args);
                case "show":
                    return OnShowCommand(args);
                case "start":
                    return OnStartCommand(args);
                case "upgrade":
                    return OnUpgradeCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnBroadcastCommand(string[] args)
        {
            string command = args[1].ToLower();
            ISerializable payload = null;
            switch (command)
            {
                case "addr":
                    payload = AddrPayload.Create(NetworkAddressWithTime.Create(new IPEndPoint(IPAddress.Parse(args[2]), ushort.Parse(args[3])), NetworkAddressWithTime.NODE_NETWORK, DateTime.UtcNow.ToTimestamp()));
                    break;
                case "block":
                    if (args[2].Length == 64 || args[2].Length == 66)
                        payload = Blockchain.Default.GetBlock(UInt256.Parse(args[2]));
                    else
                        payload = Blockchain.Default.GetBlock(uint.Parse(args[2]));
                    break;
                case "getblocks":
                case "getheaders":
                    payload = GetBlocksPayload.Create(UInt256.Parse(args[2]));
                    break;
                case "getdata":
                case "inv":
                    payload = InvPayload.Create(Enum.Parse<InventoryType>(args[2], true), args.Skip(3).Select(p => UInt256.Parse(p)).ToArray());
                    break;
                case "tx":
                    payload = LocalNode.GetTransaction(UInt256.Parse(args[2]));
                    if (payload == null)
                        payload = Blockchain.Default.GetTransaction(UInt256.Parse(args[2]));
                    break;
                case "alert":
                case "consensus":
                case "filteradd":
                case "filterload":
                case "headers":
                case "merkleblock":
                case "ping":
                case "pong":
                case "reject":
                case "verack":
                case "version":
                    Console.WriteLine($"Command \"{command}\" is not supported.");
                    return true;
            }
            foreach (RemoteNode node in LocalNode.GetRemoteNodes())
                node.EnqueueMessage(command, payload);
            return true;
        }

        private bool OnCreateCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "address":
                    return OnCreateAddressCommand(args);
                case "wallet":
                    return OnCreateWalletCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnCreateAddressCommand(string[] args)
        {
            if (Program.Wallet == null)
            {
                Console.WriteLine("You have to open the wallet first.");
                return true;
            }
            if (args.Length > 3)
            {
                Console.WriteLine("error");
                return true;
            }
            ushort count = 1;
            if (args.Length >= 3)
                count = ushort.Parse(args[2]);
            List<string> addresses = new List<string>();
            for (int i = 1; i <= count; i++)
            {
                WalletAccount account = Program.Wallet.CreateAccount();
                addresses.Add(account.Address);
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"[{i}/{count}]");
            }
            if (Program.Wallet is NEP6Wallet wallet)
                wallet.Save();
            Console.WriteLine();
            string path = "address.txt";
            Console.WriteLine($"export addresses to {path}");
            File.WriteAllLines(path, addresses);
            return true;
        }

        private bool OnCreateWalletCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("error");
                return true;
            }
            string path = args[2];
            string password = ReadPassword("password");
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            string password2 = ReadPassword("password");
            if (password != password2)
            {
                Console.WriteLine("error");
                return true;
            }
            switch (Path.GetExtension(path))
            {
                case ".db3":
                    {
                        Program.Wallet = UserWallet.Create(path, password);
                        WalletAccount account = Program.Wallet.CreateAccount();
                        Console.WriteLine($"address: {account.Address}");
                        Console.WriteLine($" pubkey: {account.GetKey().PublicKey.EncodePoint(true).ToHexString()}");
                    }
                    break;
                case ".json":
                    {
                        NEP6Wallet wallet = new NEP6Wallet(path);
                        wallet.Unlock(password);
                        WalletAccount account = wallet.CreateAccount();
                        wallet.Save();
                        Program.Wallet = wallet;
                        Console.WriteLine($"address: {account.Address}");
                        Console.WriteLine($" pubkey: {account.GetKey().PublicKey.EncodePoint(true).ToHexString()}");
                    }
                    break;
                default:
                    Console.WriteLine("Wallet files in that format are not supported, please use a .json or .db3 file extension.");
                    break;
            }
            return true;
        }

        private bool OnExportCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "blocks":
                    return OnExportBlocksCommand(args);
                case "key":
                    return OnExportKeyCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnExportBlocksCommand(string[] args)
        {
            if (args.Length > 3)
            {
                Console.WriteLine("error");
                return true;
            }
            string path = args.Length >= 3 ? args[2] : "chain.acc";
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                uint count = Blockchain.Default.Height + 1;
                uint start = 0;
                if (fs.Length > 0)
                {
                    byte[] buffer = new byte[sizeof(uint)];
                    fs.Read(buffer, 0, buffer.Length);
                    start = BitConverter.ToUInt32(buffer, 0);
                    fs.Seek(0, SeekOrigin.Begin);
                }
                if (start < count)
                    fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
                fs.Seek(0, SeekOrigin.End);
                for (uint i = start; i < count; i++)
                {
                    Block block = Blockchain.Default.GetBlock(i);
                    byte[] array = block.ToArray();
                    fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                    fs.Write(array, 0, array.Length);
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"[{i + 1}/{count}]");
                }
            }
            Console.WriteLine();
            return true;
        }

        private bool OnExportKeyCommand(string[] args)
        {
            if (Program.Wallet == null)
            {
                Console.WriteLine("You have to open the wallet first.");
                return true;
            }
            if (args.Length < 2 || args.Length > 4)
            {
                Console.WriteLine("error");
                return true;
            }
            UInt160 scriptHash = null;
            string path = null;
            if (args.Length == 3)
            {
                try
                {
                    scriptHash = Wallet.ToScriptHash(args[2]);
                }
                catch (FormatException)
                {
                    path = args[2];
                }
            }
            else if (args.Length == 4)
            {
                scriptHash = Wallet.ToScriptHash(args[2]);
                path = args[3];
            }
            string password = ReadPassword("password");
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            if (!Program.Wallet.VerifyPassword(password))
            {
                Console.WriteLine("Incorrect password");
                return true;
            }
            IEnumerable<KeyPair> keys;
            if (scriptHash == null)
                keys = Program.Wallet.GetAccounts().Where(p => p.HasKey).Select(p => p.GetKey());
            else
                keys = new[] { Program.Wallet.GetAccount(scriptHash).GetKey() };
            if (path == null)
                foreach (KeyPair key in keys)
                    Console.WriteLine(key.Export());
            else
                File.WriteAllLines(path, keys.Select(p => p.Export()));
            return true;
        }

        private bool OnHelpCommand(string[] args)
        {
            Console.Write(
                "Normal Commands:\n" +
                "\tversion\n" +
                "\thelp\n" +
                "\tclear\n" +
                "\texit\n" +
                "Wallet Commands:\n" +
                "\tcreate wallet <path>\n" +
                "\topen wallet <path>\n" +
                "\tupgrade wallet <path>\n" +
                "\trebuild index\n" +
                "\tlist address\n" +
                "\tlist asset\n" +
                "\tlist key\n" +
                "\tshow utxo [id|alias]\n" +
                "\tshow gas\n" +
                "\tclaim gas\n" +
                "\tcreate address [n=1]\n" +
                "\timport key <wif|path>\n" +
                "\texport key [address] [path]\n" +
                "\tsend <id|alias> <address> <value>|all [fee=0]\n" +
                "Node Commands:\n" +
                "\tshow state\n" +
                "\tshow node\n" +
                "\tshow pool [verbose]\n" +
                "\texport blocks [path=chain.acc]\n" +
                "Advanced Commands:\n" +
                "\tstart consensus\n" +
                "\trefresh policy\n");
            return true;
        }

        private bool OnImportCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "key":
                    return OnImportKeyCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnImportKeyCommand(string[] args)
        {
            if (args.Length > 3)
            {
                Console.WriteLine("error");
                return true;
            }
            byte[] prikey = null;
            try
            {
                prikey = Wallet.GetPrivateKeyFromWIF(args[2]);
            }
            catch (FormatException) { }
            if (prikey == null)
            {
                string[] lines = File.ReadAllLines(args[2]);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Length == 64)
                        prikey = lines[i].HexToBytes();
                    else
                        prikey = Wallet.GetPrivateKeyFromWIF(lines[i]);
                    Program.Wallet.CreateAccount(prikey);
                    Array.Clear(prikey, 0, prikey.Length);
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"[{i + 1}/{lines.Length}]");
                }
                Console.WriteLine();
            }
            else
            {
                WalletAccount account = Program.Wallet.CreateAccount(prikey);
                Array.Clear(prikey, 0, prikey.Length);
                Console.WriteLine($"address: {account.Address}");
                Console.WriteLine($" pubkey: {account.GetKey().PublicKey.EncodePoint(true).ToHexString()}");
            }
            if (Program.Wallet is NEP6Wallet wallet)
                wallet.Save();
            return true;
        }

        private bool OnListCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "address":
                    return OnListAddressCommand(args);
                case "asset":
                    return OnListAssetCommand(args);
                case "key":
                    return OnListKeyCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnClaimCommand(string[] args)
        {
            if (Program.Wallet == null)
            {
                Console.WriteLine($"Please open a wallet");
                return true;
            }

            Coins coins = new Coins(Program.Wallet, LocalNode);

            switch (args[1].ToLower())
            {
                case "gas":
                    ClaimTransaction tx = coins.Claim();
                    if (tx is ClaimTransaction)
                    {
                        Console.WriteLine($"Tranaction Suceeded: {tx.Hash.ToString()}");
                    }
                    return true;
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnShowGasCommand(string[] args)
        {
            if (Program.Wallet == null)
            {
                Console.WriteLine($"Please open a wallet");
                return true;
            }

            Coins coins = new Coins(Program.Wallet, LocalNode);
            Console.WriteLine($"unavailable: {coins.UnavailableBonus().ToString()}");
            Console.WriteLine($"  available: {coins.AvailableBonus().ToString()}");
            return true;
        }

        private bool OnListKeyCommand(string[] args)
        {
            if (Program.Wallet == null) return true;
            foreach (KeyPair key in Program.Wallet.GetAccounts().Where(p => p.HasKey).Select(p => p.GetKey()))
            {
                Console.WriteLine(key.PublicKey);
            }
            return true;
        }

        private bool OnListAddressCommand(string[] args)
        {
            if (Program.Wallet == null) return true;
            foreach (Contract contract in Program.Wallet.GetAccounts().Where(p => !p.WatchOnly).Select(p => p.Contract))
            {
                Console.WriteLine($"{contract.Address}\t{(contract.IsStandard ? "Standard" : "Nonstandard")}");
            }
            return true;
        }

        private bool OnListAssetCommand(string[] args)
        {
            if (Program.Wallet == null) return true;
            foreach (var item in Program.Wallet.GetCoins().Where(p => !p.State.HasFlag(CoinState.Spent)).GroupBy(p => p.Output.AssetId, (k, g) => new
            {
                Asset = Blockchain.Default.GetAssetState(k),
                Balance = g.Sum(p => p.Output.Value),
                Confirmed = g.Where(p => p.State.HasFlag(CoinState.Confirmed)).Sum(p => p.Output.Value)
            }))
            {
                Console.WriteLine($"       id:{item.Asset.AssetId}");
                Console.WriteLine($"     name:{item.Asset.GetName()}");
                Console.WriteLine($"  balance:{item.Balance}");
                Console.WriteLine($"confirmed:{item.Confirmed}");
                Console.WriteLine();
            }
            return true;
        }

        private bool OnOpenCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "wallet":
                    return OnOpenWalletCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        //TODO: 目前没有想到其它安全的方法来保存密码
        //所以只能暂时手动输入，但如此一来就不能以服务的方式启动了
        //未来再想想其它办法，比如采用智能卡之类的
        private bool OnOpenWalletCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("error");
                return true;
            }
            string path = args[2];
            if (!File.Exists(path))
            {
                Console.WriteLine($"File does not exist");
                return true;
            }
            string password = ReadPassword("password");
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            if (Path.GetExtension(path) == ".db3")
            {
                try
                {
                    Program.Wallet = UserWallet.Open(path, password);
                }
                catch (CryptographicException)
                {
                    Console.WriteLine($"failed to open file \"{path}\"");
                    return true;
                }
            }
            else
            {
                NEP6Wallet nep6wallet = new NEP6Wallet(path);
                try
                {
                    nep6wallet.Unlock(password);
                }
                catch (CryptographicException)
                {
                    Console.WriteLine($"failed to open file \"{path}\"");
                    return true;
                }
                Program.Wallet = nep6wallet;
            }
            return true;
        }

        private bool OnRebuildCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "index":
                    return OnRebuildIndexCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnRebuildIndexCommand(string[] args)
        {
            WalletIndexer.RebuildIndex();
            return true;
        }

        private bool OnRefreshCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "policy":
                    return OnRefreshPolicyCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnRefreshPolicyCommand(string[] args)
        {
            if (consensus == null) return true;
            consensus.RefreshPolicy();
            return true;
        }

        private bool OnSendCommand(string[] args)
        {
            if (args.Length < 4 || args.Length > 5)
            {
                Console.WriteLine("error");
                return true;
            }
            if (Program.Wallet == null)
            {
                Console.WriteLine("You have to open the wallet first.");
                return true;
            }
            string password = ReadPassword("password");
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            if (!Program.Wallet.VerifyPassword(password))
            {
                Console.WriteLine("Incorrect password");
                return true;
            }
            UIntBase assetId;
            switch (args[1].ToLower())
            {
                case "neo":
                case "ans":
                    assetId = Blockchain.GoverningToken.Hash;
                    break;
                case "gas":
                case "anc":
                    assetId = Blockchain.UtilityToken.Hash;
                    break;
                default:
                    assetId = UIntBase.Parse(args[1]);
                    break;
            }
            UInt160 scriptHash = Wallet.ToScriptHash(args[2]);
            bool isSendAll = string.Equals(args[3], "all", StringComparison.OrdinalIgnoreCase);
            Transaction tx;
            if (isSendAll)
            {
                Coin[] coins = Program.Wallet.FindUnspentCoins().Where(p => p.Output.AssetId.Equals(assetId)).ToArray();
                tx = new ContractTransaction
                {
                    Attributes = new TransactionAttribute[0],
                    Inputs = coins.Select(p => p.Reference).ToArray(),
                    Outputs = new[]
                    {
                        new TransactionOutput
                        {
                            AssetId = (UInt256)assetId,
                            Value = coins.Sum(p => p.Output.Value),
                            ScriptHash = scriptHash
                        }
                    }
                };
            }
            else
            {
                AssetDescriptor descriptor = new AssetDescriptor(assetId);
                if (!BigDecimal.TryParse(args[3], descriptor.Decimals, out BigDecimal amount))
                {
                    Console.WriteLine("Incorrect Amount Format");
                    return true;
                }
                Fixed8 fee = args.Length >= 5 ? Fixed8.Parse(args[4]) : Fixed8.Zero;
                tx = Program.Wallet.MakeTransaction(null, new[]
                {
                    new TransferOutput
                    {
                        AssetId = assetId,
                        Value = amount,
                        ScriptHash = scriptHash
                    }
                }, fee: fee);
                if (tx == null)
                {
                    Console.WriteLine("Insufficient funds");
                    return true;
                }
            }
            ContractParametersContext context = new ContractParametersContext(tx);
            Program.Wallet.Sign(context);
            if (context.Completed)
            {
                tx.Scripts = context.GetScripts();
                Program.Wallet.ApplyTransaction(tx);
                LocalNode.Relay(tx);
                Console.WriteLine($"TXID: {tx.Hash}");
            }
            else
            {
                Console.WriteLine("SignatureContext:");
                Console.WriteLine(context.ToString());
            }
            return true;
        }

        private bool OnShowCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "gas":
                    return OnShowGasCommand(args);
                case "node":
                    return OnShowNodeCommand(args);
                case "pool":
                    return OnShowPoolCommand(args);
                case "state":
                    return OnShowStateCommand(args);
                case "utxo":
                    return OnShowUtxoCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnShowNodeCommand(string[] args)
        {
            RemoteNode[] nodes = LocalNode.GetRemoteNodes();
            for (int i = 0; i < nodes.Length; i++)
            {
                Console.WriteLine($"{nodes[i].RemoteEndpoint.Address} port:{nodes[i].RemoteEndpoint.Port} listen:{nodes[i].ListenerEndpoint?.Port ?? 0} height:{nodes[i].Version?.StartHeight ?? 0} [{i + 1}/{nodes.Length}]");
            }
            return true;
        }

        private bool OnShowPoolCommand(string[] args)
        {
            bool verbose = args.Length >= 3 && args[2] == "verbose";
            Transaction[] transactions = LocalNode.GetMemoryPool().ToArray();
            if (verbose)
                foreach (Transaction tx in transactions)
                    Console.WriteLine($"{tx.Hash} {tx.GetType().Name}");
            Console.WriteLine($"total: {transactions.Length}");
            return true;
        }

        private bool OnShowStateCommand(string[] args)
        {
            uint wh = 0;
            if (Program.Wallet != null)
            {
                wh = (Program.Wallet.WalletHeight > 0) ? Program.Wallet.WalletHeight - 1 : 0;
            }
            Console.WriteLine($"Height: {wh}/{Blockchain.Default.Height}/{Blockchain.Default.HeaderHeight}, Nodes: {LocalNode.RemoteNodeCount}");
            return true;
        }

        private bool OnShowUtxoCommand(string[] args)
        {
            if (Program.Wallet == null)
            {
                Console.WriteLine("You have to open the wallet first.");
                return true;
            }
            IEnumerable<Coin> coins = Program.Wallet.FindUnspentCoins();
            if (args.Length >= 3)
            {
                UInt256 assetId;
                switch (args[2].ToLower())
                {
                    case "neo":
                    case "ans":
                        assetId = Blockchain.GoverningToken.Hash;
                        break;
                    case "gas":
                    case "anc":
                        assetId = Blockchain.UtilityToken.Hash;
                        break;
                    default:
                        assetId = UInt256.Parse(args[2]);
                        break;
                }
                coins = coins.Where(p => p.Output.AssetId.Equals(assetId));
            }
            Coin[] coins_array = coins.ToArray();
            const int MAX_SHOW = 100;
            for (int i = 0; i < coins_array.Length && i < MAX_SHOW; i++)
                Console.WriteLine($"{coins_array[i].Reference.PrevHash}:{coins_array[i].Reference.PrevIndex}");
            if (coins_array.Length > MAX_SHOW)
                Console.WriteLine($"({coins_array.Length - MAX_SHOW} more)");
            Console.WriteLine($"total: {coins_array.Length} UTXOs");
            return true;
        }

        protected internal override void OnStart(string[] args)
        {
            Blockchain.RegisterBlockchain(new LevelDBBlockchain(Settings.Default.Paths.Chain));
            if (!args.Contains("--nopeers") && File.Exists(PeerStatePath))
                using (FileStream fs = new FileStream(PeerStatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    LocalNode.LoadState(fs);
                }
            LocalNode = new LocalNode();
            Task.Run(() =>
            {
                const string acc_path = "chain.acc";
                const string acc_zip_path = acc_path + ".zip";
                if (File.Exists(acc_path))
                {
                    using (FileStream fs = new FileStream(acc_path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        ImportBlocks(fs);
                    }
                    File.Delete(acc_path);
                }
                else if (File.Exists(acc_zip_path))
                {
                    using (FileStream fs = new FileStream(acc_zip_path, FileMode.Open, FileAccess.Read, FileShare.None))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    using (Stream zs = zip.GetEntry(acc_path).Open())
                    {
                        ImportBlocks(zs);
                    }
                    File.Delete(acc_zip_path);
                }
                LocalNode.Start(Settings.Default.P2P.Port, Settings.Default.P2P.WsPort);
                bool log = false;
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "/rpc":
                        case "--rpc":
                        case "-r":
                            if (rpc == null)
                            {
                                rpc = new RpcServerWithWallet(LocalNode);
                                rpc.Start(Settings.Default.RPC.Port, Settings.Default.RPC.SslCert, Settings.Default.RPC.SslCertPassword);
                            }
                            break;
                        case "-l":
                        case "--log":
                            log = true;
                            break;
                    }
                }
                if (log)
                    LevelDBBlockchain.ApplicationExecuted += LevelDBBlockchain_ApplicationExecuted;
            });
        }

        private bool OnStartCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "consensus":
                    return OnStartConsensusCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnStartConsensusCommand(string[] args)
        {
            if (consensus != null) return true;
            if (Program.Wallet == null)
            {
                Console.WriteLine("You have to open the wallet first.");
                return true;
            }
            string log_dictionary = Path.Combine(AppContext.BaseDirectory, "Logs");
            consensus = new ConsensusWithPolicy(LocalNode, Program.Wallet, log_dictionary);
            ShowPrompt = false;
            consensus.Start();
            return true;
        }

        protected internal override void OnStop()
        {
            if (consensus != null) consensus.Dispose();
            if (rpc != null) rpc.Dispose();
            LocalNode.Dispose();
            using (FileStream fs = new FileStream(PeerStatePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                LocalNode.SaveState(fs);
            }
            Blockchain.Default.Dispose();
        }

        private bool OnUpgradeCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "wallet":
                    return OnUpgradeWalletCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnUpgradeWalletCommand(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("error");
                return true;
            }
            string path = args[2];
            if (Path.GetExtension(path) != ".db3")
            {
                Console.WriteLine("Can't upgrade the wallet file.");
                return true;
            }
            if (!File.Exists(path))
            {
                Console.WriteLine("File does not exist.");
                return true;
            }
            string password = ReadPassword("password");
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            string path_new = Path.ChangeExtension(path, ".json");
            NEP6Wallet.Migrate(path_new, path, password).Save();
            Console.WriteLine($"Wallet file upgrade complete. New wallet file has been auto-saved at: {path_new}");
            return true;
        }

        private void LevelDBBlockchain_ApplicationExecuted(object sender, ApplicationExecutedEventArgs e)
        {
            JObject json = new JObject();
            json["txid"] = e.Transaction.Hash.ToString();
            json["vmstate"] = e.VMState;
            json["gas_consumed"] = e.GasConsumed.ToString();
            json["stack"] = e.Stack.Select(p => p.ToParameter().ToJson()).ToArray();
            json["notifications"] = e.Notifications.Select(p =>
            {
                JObject notification = new JObject();
                notification["contract"] = p.ScriptHash.ToString();
                notification["state"] = p.State.ToParameter().ToJson();
                return notification;
            }).ToArray();
            Directory.CreateDirectory(Settings.Default.Paths.ApplicationLogs);
            string path = Path.Combine(Settings.Default.Paths.ApplicationLogs, $"{e.Transaction.Hash}.json");
            File.WriteAllText(path, json.ToString());
        }
    }
}
