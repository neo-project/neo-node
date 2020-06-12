using Akka.Actor;
using Neo.ConsoleService;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Neo.Wallets.SQLite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Neo.CLI
{
    public partial class MainService : ConsoleServiceBase
    {
        public event EventHandler WalletChanged;

        private Wallet currentWallet;
        public Wallet CurrentWallet
        {
            get
            {
                return currentWallet;
            }
            private set
            {
                currentWallet = value;
                WalletChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private NeoSystem neoSystem;
        public NeoSystem NeoSystem
        {
            get
            {
                return neoSystem;
            }
            private set
            {
                neoSystem = value;
            }
        }

        protected override string Prompt => "neo";
        public override string ServiceName => "NEO-CLI";

        /// <summary>
        /// Constructor
        /// </summary>
        public MainService() : base()
        {
            RegisterCommandHander<string, UInt160>(false, (str) =>
            {
                switch (str.ToLowerInvariant())
                {
                    case "neo": return SmartContract.Native.NativeContract.NEO.Hash;
                    case "gas": return SmartContract.Native.NativeContract.GAS.Hash;
                }

                // Try to parse as UInt160

                if (UInt160.TryParse(str, out var addr))
                {
                    return addr;
                }

                // Accept wallet format

                return str.ToScriptHash();
            });

            RegisterCommandHander<string, UInt256>(false, (str) => UInt256.Parse(str));
            RegisterCommandHander<string[], UInt256[]>((str) => str.Select(u => UInt256.Parse(u.Trim())).ToArray());
            RegisterCommandHander<string[], UInt160[]>((arr) =>
            {
                return arr.Select(str =>
                {
                    switch (str.ToLowerInvariant())
                    {
                        case "neo": return SmartContract.Native.NativeContract.NEO.Hash;
                        case "gas": return SmartContract.Native.NativeContract.GAS.Hash;
                    }

                    // Try to parse as UInt160

                    if (UInt160.TryParse(str, out var addr))
                    {
                        return addr;
                    }

                    // Accept wallet format

                    return str.ToScriptHash();
                })
                .ToArray();
            });

            RegisterCommandHander<string, ECPoint>((str) => ECPoint.Parse(str.Trim(), ECCurve.Secp256r1));
            RegisterCommandHander<string[], ECPoint[]>((str) => str.Select(u => ECPoint.Parse(u.Trim(), ECCurve.Secp256r1)).ToArray());
            RegisterCommandHander<string, JObject>((str) => JObject.Parse(str));
            RegisterCommandHander<string, decimal>((str) => decimal.Parse(str, CultureInfo.InvariantCulture));
            RegisterCommandHander<JObject, JArray>((obj) => (JArray)obj);

            RegisterCommand(this);
        }

        public override void RunConsole()
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;

            var cliV = Assembly.GetAssembly(typeof(Program)).GetVersion();
            var neoV = Assembly.GetAssembly(typeof(NeoSystem)).GetVersion();
            var vmV = Assembly.GetAssembly(typeof(ExecutionEngine)).GetVersion();
            Console.WriteLine($"{ServiceName} v{cliV}  -  NEO v{neoV}  -  NEO-VM v{vmV}");
            Console.WriteLine();

            base.RunConsole();
        }

        public void CreateWallet(string path, string password)
        {
            switch (Path.GetExtension(path))
            {
                case ".db3":
                    {
                        UserWallet wallet = UserWallet.Create(path, password);
                        WalletAccount account = wallet.CreateAccount();
                        Console.WriteLine($"   Address: {account.Address}");
                        Console.WriteLine($"    Pubkey: {account.GetKey().PublicKey.EncodePoint(true).ToHexString()}");
                        Console.WriteLine($"ScriptHash: {account.ScriptHash}");
                        CurrentWallet = wallet;
                    }
                    break;
                case ".json":
                    {
                        NEP6Wallet wallet = new NEP6Wallet(path);
                        wallet.Unlock(password);
                        WalletAccount account = wallet.CreateAccount();
                        wallet.Save();
                        Console.WriteLine($"   Address: {account.Address}");
                        Console.WriteLine($"    Pubkey: {account.GetKey().PublicKey.EncodePoint(true).ToHexString()}");
                        Console.WriteLine($"ScriptHash: {account.ScriptHash}");
                        CurrentWallet = wallet;
                    }
                    break;
                default:
                    Console.WriteLine("Wallet files in that format are not supported, please use a .json or .db3 file extension.");
                    break;
            }
        }

        private static IEnumerable<Block> GetBlocks(Stream stream, bool read_start = false)
        {
            using BinaryReader r = new BinaryReader(stream);
            uint start = read_start ? r.ReadUInt32() : 0;
            uint count = r.ReadUInt32();
            uint end = start + count - 1;
            if (end <= Blockchain.Singleton.Height) yield break;
            for (uint height = start; height <= end; height++)
            {
                var size = r.ReadInt32();
                if (size > Message.PayloadMaxSize)
                    throw new ArgumentException($"Block {height} exceeds the maximum allowed size");

                byte[] array = r.ReadBytes(size);
                if (height > Blockchain.Singleton.Height)
                {
                    Block block = array.AsSerializable<Block>();
                    yield return block;
                }
            }
        }

        private IEnumerable<Block> GetBlocksFromFile()
        {
            const string pathAcc = "chain.acc";
            if (File.Exists(pathAcc))
                using (FileStream fs = new FileStream(pathAcc, FileMode.Open, FileAccess.Read, FileShare.Read))
                    foreach (var block in GetBlocks(fs))
                        yield return block;

            const string pathAccZip = pathAcc + ".zip";
            if (File.Exists(pathAccZip))
                using (FileStream fs = new FileStream(pathAccZip, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                using (Stream zs = zip.GetEntry(pathAcc).Open())
                    foreach (var block in GetBlocks(zs))
                        yield return block;

            var paths = Directory.EnumerateFiles(".", "chain.*.acc", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(".", "chain.*.acc.zip", SearchOption.TopDirectoryOnly)).Select(p => new
            {
                FileName = Path.GetFileName(p),
                Start = uint.Parse(Regex.Match(p, @"\d+").Value),
                IsCompressed = p.EndsWith(".zip")
            }).OrderBy(p => p.Start);

            foreach (var path in paths)
            {
                if (path.Start > Blockchain.Singleton.Height + 1) break;
                if (path.IsCompressed)
                    using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    using (Stream zs = zip.GetEntry(Path.GetFileNameWithoutExtension(path.FileName)).Open())
                        foreach (var block in GetBlocks(zs, true))
                            yield return block;
                else
                    using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        foreach (var block in GetBlocks(fs, true))
                            yield return block;
            }
        }

        private bool NoWallet()
        {
            if (CurrentWallet != null) return false;
            Console.WriteLine("You have to open the wallet first.");
            return true;
        }

        private byte[] LoadDeploymentScript(string nefFilePath, string manifestFilePath, out UInt160 scriptHash)
        {
            if (string.IsNullOrEmpty(manifestFilePath))
            {
                manifestFilePath = Path.ChangeExtension(nefFilePath, ".manifest.json");
            }

            // Read manifest

            var info = new FileInfo(manifestFilePath);
            if (!info.Exists || info.Length >= Transaction.MaxTransactionSize)
            {
                throw new ArgumentException(nameof(manifestFilePath));
            }

            var manifest = ContractManifest.Parse(File.ReadAllBytes(manifestFilePath));

            // Read nef

            info = new FileInfo(nefFilePath);
            if (!info.Exists || info.Length >= Transaction.MaxTransactionSize)
            {
                throw new ArgumentException(nameof(nefFilePath));
            }

            NefFile file;
            using (var stream = new BinaryReader(File.OpenRead(nefFilePath), Encoding.UTF8, false))
            {
                file = stream.ReadSerializable<NefFile>();
            }

            // Basic script checks

            using (var engine = new ApplicationEngine(TriggerType.Application, null, null, 0, true))
            {
                var context = engine.LoadScript(file.Script);

                while (context.InstructionPointer <= context.Script.Length)
                {
                    // Check bad opcodes

                    var ci = context.CurrentInstruction;

                    if (ci == null || !Enum.IsDefined(typeof(OpCode), ci.OpCode))
                    {
                        throw new FormatException($"OpCode not found at {context.InstructionPointer}-{((byte)ci.OpCode).ToString("x2")}");
                    }

                    context.InstructionPointer += ci.Size;
                }
            }

            // Build script

            scriptHash = file.ScriptHash;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall(ApplicationEngine.System_Contract_Create, file.Script, manifest.ToJson().ToString());
                return sb.ToArray();
            }
        }

        public override void OnStart(string[] args)
        {
            base.OnStart(args);
            Start(args);
        }

        public override void OnStop()
        {
            base.OnStop();
            Stop();
        }

        public void OpenWallet(string path, string password)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException();
            }

            if (Path.GetExtension(path) == ".db3")
            {
                CurrentWallet = UserWallet.Open(path, password);
            }
            else
            {
                NEP6Wallet nep6wallet = new NEP6Wallet(path);
                nep6wallet.Unlock(password);
                CurrentWallet = nep6wallet;
            }
        }

        public async void Start(string[] args)
        {
            if (NeoSystem != null) return;
            bool verifyImport = true;
            for (int i = 0; i < args.Length; i++)
                switch (args[i])
                {
                    case "/noverify":
                    case "--noverify":
                        verifyImport = false;
                        break;
                }

            _ = new Logger();

            NeoSystem = new NeoSystem(Settings.Default.Storage.Engine);

            foreach (var plugin in Plugin.Plugins)
            {
                // Register plugins commands

                RegisterCommand(plugin, plugin.Name);
            }

            using (IEnumerator<Block> blocksBeingImported = GetBlocksFromFile().GetEnumerator())
            {
                while (true)
                {
                    List<Block> blocksToImport = new List<Block>();
                    for (int i = 0; i < 10; i++)
                    {
                        if (!blocksBeingImported.MoveNext()) break;
                        blocksToImport.Add(blocksBeingImported.Current);
                    }
                    if (blocksToImport.Count == 0) break;
                    await NeoSystem.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                    {
                        Blocks = blocksToImport,
                        Verify = verifyImport
                    });
                    if (NeoSystem is null) return;
                }
            }
            NeoSystem.StartNode(new ChannelsConfig
            {
                Tcp = new IPEndPoint(IPAddress.Any, Settings.Default.P2P.Port),
                WebSocket = new IPEndPoint(IPAddress.Any, Settings.Default.P2P.WsPort),
                MinDesiredConnections = Settings.Default.P2P.MinDesiredConnections,
                MaxConnections = Settings.Default.P2P.MaxConnections,
                MaxConnectionsPerAddress = Settings.Default.P2P.MaxConnectionsPerAddress
            });
            if (Settings.Default.UnlockWallet.IsActive)
            {
                try
                {
                    OpenWallet(Settings.Default.UnlockWallet.Path, Settings.Default.UnlockWallet.Password);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"Warning: wallet file \"{Settings.Default.UnlockWallet.Path}\" not found.");
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    Console.WriteLine($"Failed to open file \"{Settings.Default.UnlockWallet.Path}\"");
                }
                if (Settings.Default.UnlockWallet.StartConsensus && CurrentWallet != null)
                {
                    OnStartConsensusCommand();
                }
            }
        }

        public void Stop()
        {
            Interlocked.Exchange(ref neoSystem, null)?.Dispose();
        }

        private void WriteBlocks(uint start, uint count, string path, bool writeStart)
        {
            uint end = start + count - 1;
            using FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            if (fs.Length > 0)
            {
                byte[] buffer = new byte[sizeof(uint)];
                if (writeStart)
                {
                    fs.Seek(sizeof(uint), SeekOrigin.Begin);
                    fs.Read(buffer, 0, buffer.Length);
                    start += BitConverter.ToUInt32(buffer, 0);
                    fs.Seek(sizeof(uint), SeekOrigin.Begin);
                }
                else
                {
                    fs.Read(buffer, 0, buffer.Length);
                    start = BitConverter.ToUInt32(buffer, 0);
                    fs.Seek(0, SeekOrigin.Begin);
                }
            }
            else
            {
                if (writeStart)
                {
                    fs.Write(BitConverter.GetBytes(start), 0, sizeof(uint));
                }
            }
            if (start <= end)
                fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
            fs.Seek(0, SeekOrigin.End);
            Console.WriteLine("Export block from " + start + " to " + end);

            using (var percent = new ConsolePercent(start, end))
            {
                for (uint i = start; i <= end; i++)
                {
                    Block block = Blockchain.Singleton.GetBlock(i);
                    byte[] array = block.ToArray();
                    fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                    fs.Write(array, 0, array.Length);
                    percent.Value = i;
                }
            }
        }

        private static void WriteLineWithoutFlicker(string message = "", int maxWidth = 80)
        {
            if (message.Length > 0) Console.Write(message);
            var spacesToErase = maxWidth - message.Length;
            if (spacesToErase < 0) spacesToErase = 0;
            Console.WriteLine(new string(' ', spacesToErase));
        }

        /// <summary>
        /// Make and send transaction with script, sender
        /// </summary>
        /// <param name="script">script</param>
        /// <param name="account">sender</param>
        private void SendTransaction(byte[] script, UInt160 account = null)
        {
            List<Cosigner> signCollection = new List<Cosigner>();

            if (account != null)
            {
                using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                {
                    UInt160[] accounts = CurrentWallet.GetAccounts().Where(p => !p.Lock && !p.WatchOnly).Select(p => p.ScriptHash).Where(p => NativeContract.GAS.BalanceOf(snapshot, p).Sign > 0).ToArray();
                    foreach (var signAccount in accounts)
                    {
                        if (account.Equals(signAccount))
                        {
                            signCollection.Add(new Cosigner() { Account = signAccount });
                            break;
                        }
                    }
                }
            }

            try
            {
                Transaction tx = CurrentWallet.MakeTransaction(script, account, signCollection?.ToArray());
                Console.WriteLine($"Invoking script with: '{tx.Script.ToHexString()}'");

                using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, null, testMode: true))
                {
                    Console.WriteLine($"VM State: {engine.State}");
                    Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");
                    Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToJson()))}");
                    Console.WriteLine();
                    if (engine.State.HasFlag(VMState.FAULT))
                    {
                        Console.WriteLine("Engine faulted.");
                        return;
                    }
                }

                if (!ReadUserInput("relay tx(no|yes)").IsYes())
                {
                    return;
                }

                SignAndSendTx(tx);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Error: insufficient balance.");
                return;
            }

            return;
        }

        /// <summary>
        /// Process "invoke" command
        /// </summary>
        /// <param name="scriptHash">Script hash</param>
        /// <param name="operation">Operation</param>
        /// <param name="verificable">Transaction</param>
        /// <param name="contractParameters">Contract parameters</param>
        private StackItem OnInvokeWithResult(UInt160 scriptHash, string operation, IVerifiable verificable = null, JArray contractParameters = null, bool showStack = true)
        {
            List<ContractParameter> parameters = new List<ContractParameter>();

            if (contractParameters != null)
            {
                foreach (var contractParameter in contractParameters)
                {
                    parameters.Add(ContractParameter.FromJson(contractParameter));
                }
            }

            byte[] script;

            using (ScriptBuilder scriptBuilder = new ScriptBuilder())
            {
                scriptBuilder.EmitAppCall(scriptHash, operation, parameters.ToArray());
                script = scriptBuilder.ToArray();
                Console.WriteLine($"Invoking script with: '{script.ToHexString()}'");
            }

            if (verificable is Transaction tx)
            {
                tx.Script = script;
            }

            using (ApplicationEngine engine = ApplicationEngine.Run(script, verificable, testMode: true))
            {
                Console.WriteLine($"VM State: {engine.State}");
                Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NativeContract.GAS.Decimals)}");

                if (showStack)
                    Console.WriteLine($"Result Stack: {new JArray(engine.ResultStack.Select(p => p.ToJson()))}");

                if (engine.State.HasFlag(VMState.FAULT) || !engine.ResultStack.TryPop(out VM.Types.StackItem ret))
                {
                    Console.WriteLine("Engine faulted.");
                    return null;
                }

                return ret;
            }
        }
    }
}
