using Akka.Actor;
using Microsoft.Extensions.Configuration;
using Neo.Cli.Extensions;
using Neo.Consensus;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Capabilities;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Services;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Neo.Wallets.SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECCurve = Neo.Cryptography.ECC.ECCurve;
using ECPoint = Neo.Cryptography.ECC.ECPoint;
using Neo.SmartContract.Native.Tokens;

namespace Neo.Shell
{
    internal class MainService : ConsoleServiceBase
    {
        private NeoSystem system;

		private Dictionary<string, string> knownSmartContracts = new Dictionary<string, string>() {
			{ "neo", "0x43cf98eddbe047e198a3e5d57006311442a0ca15" },
			{ "gas", "0xa1760976db5fcdfab2a9930e8f6ce875b2d18225" },
			{ "policy", "0x9c5699b260bd468e2160dd5d45dfd2686bba8b77" },
		};

        protected override string Prompt => "neo";
        public override string ServiceName => "NEO-CLI";

        private static bool NoWallet()
        {
            if (Program.Wallet != null) return false;
            Console.WriteLine("You have to open the wallet first.");
            return true;
        }

        protected override bool OnCommand(string[] args)
        {
            if (Plugin.SendMessage(args)) return true;
            switch (args[0].ToLower())
            {
                case "broadcast":
                    return OnBroadcastCommand(args);
                case "relay":
                    return OnRelayCommand(args);
                case "sign":
                    return OnSignCommand(args);
                case "change":
                    return OnChangeCommand(args);
                case "create":
                    return OnCreateCommand(args);
                case "export":
                    return OnExportCommand(args);
                case "help":
                    return OnHelpCommand(args);
                case "plugins":
                    return OnPluginsCommand(args);
                case "import":
                    return OnImportCommand(args);
                case "list":
                    return OnListCommand(args);
                case "open":
                    return OnOpenCommand(args);
                case "close":
                    return OnCloseCommand(args);
                case "send":
                    return OnSendCommand(args);
                case "show":
                    return OnShowCommand(args);
                case "start":
                    return OnStartCommand(args);
                case "upgrade":
                    return OnUpgradeCommand(args);
                case "deploy":
                    return OnDeployCommand(args);
                case "invoke":
                    return OnInvokeCommand(args);
                case "install":
                    return OnInstallCommand(args);
                case "uninstall":
                    return OnUnInstallCommand(args);
				case "tool":
					return OnToolCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

		private bool OnToolCommand(string[] args)
		{
            switch (args[1].ToLower())
            {
                case "hextostr":
                case "hextostring":
                    return OnHexToStr(args);
                case "stringtohex":
                    return OnStringToHex(args);
                case "hextonumber":
                    return OnHexToNumber(args);
                case "numbertohex":
                    return OnNumberToHex(args);
                case "addrtoscript":
                case "addresstoscript":
                case "addresstoscripthash":
                    return OnAddressToScript(args);
                case "scripttoaddr":
                case "scripthashtoaddr":
                case "scripthashtoaddress":
                    return OnScripthashToAddress(args);
                default:
                    return base.OnCommand(args);
            }
		}

        /// <summary>
        /// Prints a string in hex (transfer -> 7472616e73666572)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnStringToHex(string[] args)
        {
            var strParam = args[2];
            var bytesParam = Encoding.UTF8.GetBytes(strParam);
            Console.WriteLine($"Number to Hex: {bytesParam.ToHexString()}");
            return true;
        }

        /// <summary>
        /// Converts an hexadecimal value to an UTF-8 string (7472616e73666572 -> transfer)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnHexToStr(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Invalid Parameters");
            }
            else
            {
                var hexString = args[2];
                var bytes = hexString.HexToBytes();
                var utf8String = Encoding.UTF8.GetString(bytes);
                Console.WriteLine($"Hex to String: {utf8String}");
            }

            return true;
        }


        /// <summary>
        /// Converts an address to its script hash (7472616e73666572 -> ARvw7LYLTUgUurzU2YMioB922pur7mVCjv)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnAddressToScript(string[] args)
        {
            var address = args[2];
            Console.WriteLine($"Address to ScriptHash: {address.ToScriptHash()}");
            return true;
        }

        /// <summary>
        /// Converts an script hash to its equivalent address  (ARvw7LYLTUgUurzU2YMioB922pur7mVCjv -> transfer)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnScripthashToAddress(string[] args)
        {
            var scriptHash = UInt160.Parse(args[2]);
            var hexScript = scriptHash.ToAddress();
            Console.WriteLine($"ScriptHash to Address: {hexScript}");
            return true;
        }

        /// <summary>
        /// Prints the desired number in hex format
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnNumberToHex(string[] args)
        {
            var strParam = args[2];
            var numberParam = BigInteger.Parse(strParam);
            Console.WriteLine($"Number to Hex: {numberParam.ToByteArray().ToHexString()}");
            return true;
        }

        /// <summary>
        /// Converts a hex value and print it as a number
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnHexToNumber(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Invalid Parameters");
            }
            else
            {
                var hexString = args[2];
                if(hexString.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                {
                    hexString = hexString.Substring(2);
                }
                var bytes = hexString.HexToBytes();
                var number = new BigInteger(bytes);
                Console.WriteLine($"Hex to Number: {number}");
            }

            return true;
        }

        private bool OnBroadcastCommand(string[] args)
        {
            if (!Enum.TryParse(args[1], true, out MessageCommand command))
            {
                Console.WriteLine($"Command \"{args[1]}\" is not supported.");
                return true;
            }
            ISerializable payload = null;
            switch (command)
            {
                case MessageCommand.Addr:
                    payload = AddrPayload.Create(NetworkAddressWithTime.Create(IPAddress.Parse(args[2]), DateTime.UtcNow.ToTimestamp(), new FullNodeCapability(), new ServerCapability(NodeCapabilityType.TcpServer, ushort.Parse(args[3]))));
                    break;
                case MessageCommand.Block:
                    if (args[2].Length == 64 || args[2].Length == 66)
                        payload = Blockchain.Singleton.GetBlock(UInt256.Parse(args[2]));
                    else
                        payload = Blockchain.Singleton.GetBlock(uint.Parse(args[2]));
                    break;
                case MessageCommand.GetBlocks:
                case MessageCommand.GetHeaders:
                    payload = GetBlocksPayload.Create(UInt256.Parse(args[2]));
                    break;
                case MessageCommand.GetData:
                case MessageCommand.Inv:
                    payload = InvPayload.Create(Enum.Parse<InventoryType>(args[2], true), args.Skip(3).Select(UInt256.Parse).ToArray());
                    break;
                case MessageCommand.Transaction:
                    payload = Blockchain.Singleton.GetTransaction(UInt256.Parse(args[2]));
                    break;
                default:
                    Console.WriteLine($"Command \"{command}\" is not supported.");
                    return true;
            }
            system.LocalNode.Tell(Message.Create(command, payload));
            return true;
        }

        private bool OnDeployCommand(string[] args)
        {
            if (NoWallet()) return true;
            byte[] script = LoadDeploymentScript(
                /* filePath */ args[1],
                /* hasStorage */ args[2].ToBool(),
                /* isPayable */ args[3].ToBool(),
                /* scriptHash */ out var scriptHash);

            Transaction tx;
            try
            {
                tx = Program.Wallet.MakeTransaction(script);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Engine faulted.");
                return true;
            }
            Console.WriteLine($"Script hash: {scriptHash.ToString()}");
            Console.WriteLine($"Gas: {tx.SystemFee}");
            Console.WriteLine();
            return SignAndSendTx(tx);
        }

		private Transaction BuildInvocationTransaction(string[] args)
		{
			Transaction tx = new Transaction
			{
				Sender = UInt160.Zero,
				Attributes = new TransactionAttribute[0],
				Witnesses = new Witness[0]
			};

			//TODO Use manifest
			var scriptHash = UInt160.Parse(args[1]);
			var operation = args[2];
			List<ContractParameter> contractParameters = ParseParameters(args);

			if (operation.Equals("transfer", StringComparison.CurrentCultureIgnoreCase))
			{
				tx.Sender = (UInt160)contractParameters[0].Value;
				var cosigner = new Cosigner();
				cosigner.Scopes = WitnessScope.CalledByEntry;
				cosigner.Account = tx.Sender;
				tx.Cosigners = new Cosigner[] { cosigner };
			}

			using (ScriptBuilder scriptBuilder = new ScriptBuilder())
			{
				scriptBuilder.EmitAppCall(scriptHash, operation, contractParameters.ToArray());
				tx.Script = scriptBuilder.ToArray();
			}

			return tx;
		}

        private bool OnInvokeCommand(string[] args)
        {
			if (knownSmartContracts.ContainsKey(args[1]))
			{
				args[1] = knownSmartContracts[args[1]];
			}

			Transaction tx = BuildInvocationTransaction(args);
            ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true);

            Console.WriteLine($"VM State: {engine.State}");
            Console.WriteLine($"Gas Consumed: {new BigDecimal(engine.GasConsumed, NeoToken.GAS.Decimals)}");
			if (engine.ResultStack.Count == 1)
			{
				using (var snapshot = Blockchain.Singleton.GetSnapshot())
				{
					var scriptHash = UInt160.Parse(args[1]);
					var contract = snapshot.Contracts.TryGet(scriptHash);
					var method = contract.Manifest.Abi.Methods.First(m => m.Name.Equals(args[2]));
					var result = engine.ResultStack.First();
					switch (method.ReturnType)
					{
						case ContractParameterType.String:
							Console.WriteLine($"Result: { result.GetString() }");
							break;
						case ContractParameterType.Integer:
							Console.WriteLine($"Result: { result.GetBigInteger() }");
							break;
						case ContractParameterType.Boolean:
							Console.WriteLine($"Result: { result.ToBoolean() }");
							break;
					}
				}
			}
			else
			{
				Console.WriteLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
			}

			if (engine.Notifications.Count != 0)
			{
				Console.WriteLine("Notifications:");
				foreach (var notification in engine.Notifications)
				{
					Console.WriteLine(notification.ToCLIString());
				}
			}

            Console.WriteLine();
            if (engine.State.HasFlag(VMState.FAULT))
            {
                Console.WriteLine("Engine faulted.");
                return true;
            }
            if (NoWallet()) return true;
            try
            {
                tx = Program.Wallet.MakeTransaction(tx.Script, cosigners: tx.Cosigners);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Error: insufficient balance.");
                return true;
            }
            if (!ReadUserInput("relay tx(no|yes)").IsYes())
            {
                return true;
            }

            return SignAndSendTx(tx);
        }

		private List<ContractParameter> ParseParameters(string[] args)
		{
			var contractParameters = new List<ContractParameter>();
			for (int i = 3; i < args.Length; i++)
			{
				var arg = args[i];
				bool isNumeric = BigInteger.TryParse(arg, out BigInteger amount);
				if (arg.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
				{
					arg = arg.Substring(2);
					if (arg.Length == 64)
					{
						contractParameters.Add(new ContractParameter()
						{
							Type = ContractParameterType.Hash256,
							Value = UInt256.Parse(arg)
						});
					}
					else
					{
						contractParameters.Add(new ContractParameter()
						{
							Type = ContractParameterType.Hash160,
							Value = UInt160.Parse(arg)
						});
					}

				}
				else if (arg.IsAddress())
				{
					contractParameters.Add(new ContractParameter()
					{
						Type = ContractParameterType.Hash160,
						Value = arg.ToScriptHash()
					});
				}
				else if(isNumeric)
				{
					contractParameters.Add(new ContractParameter()
					{
						Type = ContractParameterType.Integer,
						Value = amount
					});
				}
				else
				{
					contractParameters.Add(new ContractParameter()
					{
						Type = ContractParameterType.String,
						Value = arg
					});
				}
			}
			return contractParameters;
		}

		private byte[] LoadDeploymentScript(string nefFilePath, bool hasStorage, bool isPayable, out UInt160 scriptHash)
        {
            var info = new FileInfo(nefFilePath);
            if (!info.Exists || info.Length >= Transaction.MaxTransactionSize)
            {
                throw new ArgumentException(nameof(nefFilePath));
            }

            NefFile file;
            using (var stream = new BinaryReader(File.OpenRead(nefFilePath), Encoding.UTF8, false))
            {
                file = stream.ReadSerializable<NefFile>();
            }
            scriptHash = file.ScriptHash;

            ContractFeatures properties = ContractFeatures.NoProperty;
            if (hasStorage) properties |= ContractFeatures.HasStorage;
            if (isPayable) properties |= ContractFeatures.Payable;
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

                    switch (ci.OpCode)
                    {
                        case OpCode.SYSCALL:
                            {
                                // Check bad syscalls (NEO2)

                                if (!InteropService.SupportedMethods().ContainsKey(ci.TokenU32))
                                {
                                    throw new FormatException($"Syscall not found {ci.TokenU32.ToString("x2")}. Are you using a NEO2 smartContract?");
                                }
                                break;
                            }
                    }

                    context.InstructionPointer += ci.Size;
                }
            }

            // Build script

            scriptHash = file.ScriptHash;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall(InteropService.Neo_Contract_Create, file.Script, properties);
                return sb.ToArray();
            }
        }

        public bool SignAndSendTx(Transaction tx)
        {
            ContractParametersContext context;
            try
            {
                context = new ContractParametersContext(tx);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Error creating contract params: {ex}");
                throw;
            }
			
            Program.Wallet.Sign(context);
            string msg;
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();

                system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });

                msg = $"Signed and relayed transaction with hash={tx.Hash}";
                Console.WriteLine(msg);
                return true;
            }

            msg = $"Failed sending transaction with hash={tx.Hash}";
            Console.WriteLine(msg);
            return true;
        }

        private bool OnRelayCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("You must input JSON object to relay.");
                return true;
            }
            var jsonObjectToRelay = string.Join(string.Empty, args.Skip(1));
            if (string.IsNullOrWhiteSpace(jsonObjectToRelay))
            {
                Console.WriteLine("You must input JSON object to relay.");
                return true;
            }
            try
            {
                ContractParametersContext context = ContractParametersContext.Parse(jsonObjectToRelay);
                if (!context.Completed)
                {
                    Console.WriteLine("The signature is incomplete.");
                    return true;
                }
                if (!(context.Verifiable is Transaction tx))
                {
                    Console.WriteLine($"Only support to relay transaction.");
                    return true;
                }
                tx.Witnesses = context.GetWitnesses();
                system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                Console.WriteLine($"Data relay success, the hash is shown as follows:\r\n{tx.Hash}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"One or more errors occurred:\r\n{e.Message}");
            }
            return true;
        }

        private bool OnSignCommand(string[] args)
        {
            if (NoWallet()) return true;

            if (args.Length < 2)
            {
                Console.WriteLine("You must input JSON object pending signature data.");
                return true;
            }
            var jsonObjectToSign = string.Join(string.Empty, args.Skip(1));
            if (string.IsNullOrWhiteSpace(jsonObjectToSign))
            {
                Console.WriteLine("You must input JSON object pending signature data.");
                return true;
            }
            try
            {
                ContractParametersContext context = ContractParametersContext.Parse(jsonObjectToSign);
                if (!Program.Wallet.Sign(context))
                {
                    Console.WriteLine("The private key that can sign the data is not found.");
                    return true;
                }
                Console.WriteLine($"Signed Output:\r\n{context}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"One or more errors occurred:\r\n{e.Message}");
            }
            return true;
        }

        private bool OnChangeCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "view":
                    return OnChangeViewCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnChangeViewCommand(string[] args)
        {
            if (args.Length != 3) return false;
            if (!byte.TryParse(args[2], out byte viewnumber)) return false;
            system.Consensus?.Tell(new ConsensusService.SetViewNumber { ViewNumber = viewnumber });
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
            if (NoWallet()) return true;
            if (args.Length > 3)
            {
                Console.WriteLine("error");
                return true;
            }

            string path = "address.txt";
            if (File.Exists(path))
            {
                if (!ReadUserInput($"The file '{path}' already exists, do you want to overwrite it? (yes|no)", false).IsYes())
                {
                    return true;
                }
            }

            ushort count;
            if (args.Length >= 3)
                count = ushort.Parse(args[2]);
            else
                count = 1;

            int x = 0;
            List<string> addresses = new List<string>();

            Parallel.For(0, count, (i) =>
            {
                WalletAccount account = Program.Wallet.CreateAccount();

                lock (addresses)
                {
                    x++;
                    addresses.Add(account.Address);
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"[{x}/{count}]");
                }
            });

            if (Program.Wallet is NEP6Wallet wallet)
                wallet.Save();
            Console.WriteLine();
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
            if (system.RpcServer != null)
            {
                if (!ReadUserInput("Warning: Opening the wallet with RPC turned on could result in asset loss. Are you sure you want to do this? (yes|no)", false).IsYes())
                {
                    return true;
                }
            }
            string path = args[2];
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            string password2 = ReadUserInput("password", true);
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
                        if (system.RpcServer != null)
                            system.RpcServer.Wallet = Program.Wallet;
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
                        if (system.RpcServer != null)
                            system.RpcServer.Wallet = Program.Wallet;
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
                case "key":
                    return OnExportKeyCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnExportKeyCommand(string[] args)
        {
            if (NoWallet()) return true;
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
                    scriptHash = args[2].ToScriptHash();
                }
                catch (FormatException)
                {
                    path = args[2];
                }
            }
            else if (args.Length == 4)
            {
                scriptHash = args[2].ToScriptHash();
                path = args[3];
            }
            if (File.Exists(path))
            {
                Console.WriteLine($"Error: File '{path}' already exists");
                return true;
            }
            string password = ReadUserInput("password", true);
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
                "\thelp [plugin-name]\n" +
                "\tclear\n" +
                "\texit\n" +
                "Wallet Commands:\n" +
                "\tcreate wallet <path>\n" +
                "\topen wallet <path>\n" +
                "\tclose wallet\n" +
                "\tupgrade wallet <path>\n" +
                "\tlist address\n" +
                "\tlist asset\n" +
                "\tlist key\n" +
                "\tshow gas\n" +
                "\tcreate address [n=1]\n" +
                "\timport key <wif|path>\n" +
                "\texport key [address] [path]\n" +
                "\timport multisigaddress m pubkeys...\n" +
                "\tsend <id|alias> <address> <value>\n" +
                "\tsign <jsonObjectToSign>\n" +
                "Contract Commands:\n" +
                "\tdeploy <nefFilePath> [manifestFile]\n" +
                "\tinvoke <scripthash> <command> [optionally quoted params separated by space]\n" +
                "Node Commands:\n" +
                "\tshow state\n" +
                "\tshow pool [verbose]\n" +
                "\trelay <jsonObjectToSign>\n" +
                "Show Commands:\n" +
                "\tshow block <index/hash>\n" +
                "\tshow transaction <hash>\n" +
                "\tshow last-transactions <1 - 100>\n" +
                "\tshow contract <script hash>\n" +
                "Tool Commands:\n" +
                "\ttool addressToScript <address>\n" +
                "\ttool scriptToAddress <scriptHash>\n" +
                "\ttool hexToString <hex string>\n" +
                "\ttool stringToHex <scriptHash>\n" +
                "\ttool hexToNumber <scriptHash>\n" +
                "\ttool numberToHex <number>\n" +
                "Plugin Commands:\n" +
                "\tplugins\n" +
                "\tinstall <pluginName>\n" +
                "\tuninstall <pluginName>\n" +
                "Advanced Commands:\n" +
                "\tstart consensus\n");

            return true;
        }

        private bool OnPluginsCommand(string[] args)
        {
            if (Plugin.Plugins.Count > 0)
            {
                Console.WriteLine("Loaded plugins:");
                Plugin.Plugins.ForEach(p => Console.WriteLine("\t" + p.Name));
            }
            else
            {
                Console.WriteLine("No loaded plugins");
            }
            return true;
        }

        private bool OnImportCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "key":
                    return OnImportKeyCommand(args);
                case "multisigaddress":
                    return OnImportMultisigAddress(args);
                default:
                    return base.OnCommand(args);
            }
        }

        private bool OnImportMultisigAddress(string[] args)
        {
            if (NoWallet()) return true;

            if (args.Length < 4)
            {
                Console.WriteLine("Error. Invalid parameters.");
                return true;
            }

            int m = int.Parse(args[2]);
            int n = args.Length - 3;

            if (m < 1 || m > n || n > 1024)
            {
                Console.WriteLine("Error. Invalid parameters.");
                return true;
            }

            ECPoint[] publicKeys = args.Skip(3).Select(p => ECPoint.Parse(p, ECCurve.Secp256r1)).ToArray();

            Contract multiSignContract = Contract.CreateMultiSigContract(m, publicKeys);
            KeyPair keyPair = Program.Wallet.GetAccounts().FirstOrDefault(p => p.HasKey && publicKeys.Contains(p.GetKey().PublicKey))?.GetKey();

            WalletAccount account = Program.Wallet.CreateAccount(multiSignContract, keyPair);
            if (Program.Wallet is NEP6Wallet wallet)
                wallet.Save();

            Console.WriteLine("Multisig. Addr.: " + multiSignContract.Address);

            return true;
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
                var file = new FileInfo(args[2]);

                if (!file.Exists)
                {
                    Console.WriteLine($"Error: File '{file.FullName}' doesn't exists");
                    return true;
                }

                if (file.Length > 1024 * 1024)
                {
                    if (!ReadUserInput($"The file '{file.FullName}' is too big, do you want to continue? (yes|no)", false).IsYes())
                    {
                        return true;
                    }
                }

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

        private bool OnShowGasCommand(string[] args)
        {
            if (NoWallet()) return true;
            BigInteger gas = BigInteger.Zero;
            using (SnapshotView snapshot = Blockchain.Singleton.GetSnapshot())
                foreach (UInt160 account in Program.Wallet.GetAccounts().Select(p => p.ScriptHash))
                {
                    gas += NativeContract.NEO.UnclaimedGas(snapshot, account, snapshot.Height + 1);
                }
            Console.WriteLine($"unclaimed gas: {new BigDecimal(gas, NativeContract.GAS.Decimals)}");
            return true;
        }

        private bool OnListKeyCommand(string[] args)
        {
            if (NoWallet()) return true;
            foreach (KeyPair key in Program.Wallet.GetAccounts().Where(p => p.HasKey).Select(p => p.GetKey()))
            {
                Console.WriteLine(key.PublicKey);
            }
            return true;
        }

        private bool OnListAddressCommand(string[] args)
        {
            if (NoWallet()) return true;

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                foreach (Contract contract in Program.Wallet.GetAccounts().Where(p => !p.WatchOnly).Select(p => p.Contract))
                {
                    var type = "Nonstandard";

                    if (contract.Script.IsMultiSigContract(out _, out _))
                    {
                        type = "MultiSignature";
                    }
                    else if (contract.Script.IsSignatureContract())
                    {
                        type = "Standard";
                    }
                    else if (snapshot.Contracts.TryGet(contract.ScriptHash) != null)
                    {
                        type = "Deployed-Nonstandard";
                    }

                    Console.WriteLine($"{contract.Address}\t{type}");
					Console.WriteLine($"Hash: {contract.ScriptHash}");
				}
            }

            return true;
        }

        private bool OnListAssetCommand(string[] args)
        {
            if (NoWallet()) return true;
            foreach (UInt160 account in Program.Wallet.GetAccounts().Select(p => p.ScriptHash))
            {
                Console.WriteLine(account.ToAddress());
                Console.WriteLine($"NEO: {Program.Wallet.GetBalance(NativeContract.NEO.Hash, account)}");
                Console.WriteLine($"GAS: {Program.Wallet.GetBalance(NativeContract.GAS.Hash, account)}");
                Console.WriteLine();
            }
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine("Total:   " + "NEO: " + Program.Wallet.GetAvailable(NativeContract.NEO.Hash) + "    GAS: " + Program.Wallet.GetAvailable(NativeContract.GAS.Hash));
            Console.WriteLine();
            Console.WriteLine("NEO hash: " + NativeContract.NEO.Hash);
            Console.WriteLine("GAS hash: " + NativeContract.GAS.Hash);
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
            if (system.RpcServer != null)
            {
                if (!ReadUserInput("Warning: Opening the wallet with RPC turned on could result in asset loss. Are you sure you want to do this? (yes|no)", false).IsYes())
                {
                    return true;
                }
            }
            string path = args[2];
            if (!File.Exists(path))
            {
                Console.WriteLine($"File does not exist");
                return true;
            }
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            try
            {
                Program.Wallet = OpenWallet(path, password);
            }
            catch (CryptographicException)
            {
                Console.WriteLine($"failed to open file \"{path}\"");
            }
            if (system.RpcServer != null)
                system.RpcServer.Wallet = Program.Wallet;
            return true;
        }

        /// <summary>
        /// process "close" command
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnCloseCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "wallet":
                    return OnCloseWalletCommand(args);
                default:
                    return base.OnCommand(args);
            }
        }

        /// <summary>
        /// process "close wallet" command
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool OnCloseWalletCommand(string[] args)
        {
            if (Program.Wallet == null)
            {
                Console.WriteLine($"Wallet is not opened");
                return true;
            }
            Program.Wallet = null;
            if (system.RpcServer != null)
            {
                system.RpcServer.Wallet = null;
            }
            Console.WriteLine($"Wallet is closed");
            return true;
        }

        private bool OnSendCommand(string[] args)
        {
            if (args.Length < 4 || args.Length > 5)
            {
                Console.WriteLine("error");
                return true;
            }
            if (NoWallet()) return true;
            string password = ReadUserInput("password", true);
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
            UInt160 assetId;
            switch (args[1].ToLower())
            {
                case "neo":
                    assetId = NativeContract.NEO.Hash;
                    break;
                case "gas":
                    assetId = NativeContract.GAS.Hash;
                    break;
                default:
                    assetId = UInt160.Parse(args[1]);
                    break;
            }
            UInt160 to = args[2].ToScriptHash();
            Transaction tx;
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            if (!BigDecimal.TryParse(args[3], descriptor.Decimals, out BigDecimal amount) || amount.Sign <= 0)
            {
                Console.WriteLine("Incorrect Amount Format");
                return true;
            }
            tx = Program.Wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to
                }
            });

            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return true;
            }

            ContractParametersContext context = new ContractParametersContext(tx);
            Program.Wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                system.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
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
                case "pool":
                    return OnShowPoolCommand(args);
                case "state":
                    return OnShowStateCommand(args);
				case "block":
					return OnShowBlock(args);
				case "transaction":
					return OnShowTransaction(args);
				case "contract":
					return OnShowContract(args);
				case "last-transactions":
					return OnShowLastTransactions(args);
				default:
                    return base.OnCommand(args);
            }
        }

        /// <summary>
        /// Prints the last transactions in the blockchain
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
		private bool OnShowLastTransactions(string[] args)
		{
			if (args.Length != 3)
			{
				Console.WriteLine("Insuficient arguments");
			}
			else
			{
				int desiredCount = int.Parse(args[2]);
				if (desiredCount > 100)
				{
					Console.WriteLine("Maxium 100 transactions");
					return true;
				}
				using (var snapshot = Blockchain.Singleton.GetSnapshot())
				{
					var blockHash = snapshot.CurrentBlockHash;
					var countedTransactions = 0;
                    var countedBlocks = 0;
                    var maxBlocks = 10000;
					Block block = snapshot.GetBlock(blockHash);
					do
					{
						foreach (var tx in block.Transactions)
						{
							Console.WriteLine(tx.ToCLIString(block.Timestamp));
                            countedTransactions++;
                            if (countedTransactions == desiredCount)
                                return true;
                        }
						
						block = snapshot.GetBlock(block.PrevHash);
                        countedBlocks++;
                        if (countedBlocks == maxBlocks)
                        {
                            var shouldContinue = ReadUserInput("Max searched blocks reached (10000), continue? ").IsYes();
                            if (shouldContinue)
                            {
                                countedBlocks = 0;
                            }
                            else
                            {
                                break;
                            }
                        }
                    } while (block != null && desiredCount > countedTransactions);
				}
			}

			return true;
		}

        /// <summary>
        /// Prints the SmartContract ABI
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
		private bool OnShowContract(string[] args)
		{
			if (args.Length != 3)
			{
				Console.WriteLine("Missing contract hash");
			}
			else
			{
				string contractHash = args[2];
				if (knownSmartContracts.ContainsKey(contractHash))
				{
					contractHash = knownSmartContracts[contractHash];
				}

				using (var snapshot = Blockchain.Singleton.GetSnapshot())
				{
					var contract160 = UInt160.Parse(contractHash);
					var smartContract = snapshot.Contracts.TryGet(contract160);
					if (smartContract != null)
					{
						Console.WriteLine(smartContract.ToCLIString());
					}
				}
			}

			return true;
		}


        /// <summary>
        /// Find and print a transaction using its hash
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
		private bool OnShowTransaction(string[] args)
		{
			if (args.Length != 3)
			{
				Console.WriteLine("Missing transaction hash");
			}
			else
			{
				string transactionHash = args[2];
				using (var snapshot = Blockchain.Singleton.GetSnapshot())
				{
					var tx256 = UInt256.Parse(transactionHash);
					var tx = snapshot.GetTransaction(tx256);
					if (tx != null)
					{
						Console.WriteLine(tx.ToCLIString());
					}
				}
			}

			return true;
		}

		/// <summary>
        /// Finds and print the block using it's scripthash or index
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
		private bool OnShowBlock(string[] args)
		{
			if (args.Length != 3)
			{
				Console.WriteLine("Missing block index or hash");
			}
			else
			{
				string blockId = args[2];
				if (blockId.Length == 66)
				{
					var blockHash = UInt256.Parse(blockId);
					if (Blockchain.Singleton.ContainsBlock(blockHash))
					{
						var block = Blockchain.Singleton.GetBlock(blockHash);
						Console.WriteLine($"Block: {block.ToCLIString()}");
					}
					else
					{
						Console.WriteLine("Block not found");
					}
				}
				else
				{
					var blockIndex = uint.Parse(blockId);
                    var header = Blockchain.Singleton.GetBlock(blockIndex);
					if (header != null)
					{
						Console.WriteLine($"Block: {header.ToCLIString()}");
					}
				}
			}

			return true;
		}

		private bool OnShowPoolCommand(string[] args)
        {
            bool verbose = args.Length >= 3 && args[2] == "verbose";
            if (verbose)
            {
                Blockchain.Singleton.MemPool.GetVerifiedAndUnverifiedTransactions(
                    out IEnumerable<Transaction> verifiedTransactions,
                    out IEnumerable<Transaction> unverifiedTransactions);
                Console.WriteLine("Verified Transactions:");
                foreach (Transaction tx in verifiedTransactions)
                    Console.WriteLine($" {tx.Hash} {tx.GetType().Name} {tx.NetworkFee} GAS_NetFee");
                Console.WriteLine("Unverified Transactions:");
                foreach (Transaction tx in unverifiedTransactions)
                    Console.WriteLine($" {tx.Hash} {tx.GetType().Name} {tx.NetworkFee} GAS_NetFee");
            }
            Console.WriteLine($"total: {Blockchain.Singleton.MemPool.Count}, verified: {Blockchain.Singleton.MemPool.VerifiedCount}, unverified: {Blockchain.Singleton.MemPool.UnVerifiedCount}");
            return true;
        }

        private bool OnShowStateCommand(string[] args)
        {
            var cancel = new CancellationTokenSource();

            Console.CursorVisible = false;
            Console.Clear();
            Task broadcast = Task.Run(async () =>
            {
                while (!cancel.Token.IsCancellationRequested)
                {
                    system.LocalNode.Tell(Message.Create(MessageCommand.Ping, PingPayload.Create(Blockchain.Singleton.Height)));
                    await Task.Delay(Blockchain.TimePerBlock, cancel.Token);
                }
            });
            Task task = Task.Run(async () =>
            {
                int maxLines = 0;

                while (!cancel.Token.IsCancellationRequested)
                {
                    Console.SetCursorPosition(0, 0);
                    WriteLineWithoutFlicker($"block: {Blockchain.Singleton.Height}/{Blockchain.Singleton.HeaderHeight}  connected: {LocalNode.Singleton.ConnectedCount}  unconnected: {LocalNode.Singleton.UnconnectedCount}", Console.WindowWidth - 1);

                    int linesWritten = 1;
                    foreach (RemoteNode node in LocalNode.Singleton.GetRemoteNodes().OrderByDescending(u => u.LastBlockIndex).Take(Console.WindowHeight - 2).ToArray())
                    {
                        Console.WriteLine(
                            $"  ip: {node.Remote.Address.ToString().PadRight(15)}\tport: {node.Remote.Port.ToString().PadRight(5)}\tlisten: {node.ListenerTcpPort.ToString().PadRight(5)}\theight: {node.LastBlockIndex.ToString().PadRight(7)}");
                        linesWritten++;
                    }

                    maxLines = Math.Max(maxLines, linesWritten);

                    while (linesWritten < maxLines)
                    {
                        WriteLineWithoutFlicker("", Console.WindowWidth - 1);
                        maxLines--;
                    }

                    await Task.Delay(500, cancel.Token);
                }
            });
            Console.ReadLine();
            cancel.Cancel();
            try { Task.WaitAll(task, broadcast); } catch { }
            Console.WriteLine();
            Console.CursorVisible = true;
            return true;
        }

        protected internal override void OnStart(string[] args)
        {
            bool useRPC = false;
            for (int i = 0; i < args.Length; i++)
                switch (args[i])
                {
                    case "/rpc":
                    case "--rpc":
                    case "-r":
                        useRPC = true;
                        break;
                    case "/testnet":
                    case "--testnet":
                    case "-t":
                        ProtocolSettings.Initialize(new ConfigurationBuilder().AddJsonFile("protocol.testnet.json").Build());
                        Settings.Initialize(new ConfigurationBuilder().AddJsonFile("config.testnet.json").Build());
                        break;
                    case "/mainnet":
                    case "--mainnet":
                    case "-m":
                        ProtocolSettings.Initialize(new ConfigurationBuilder().AddJsonFile("protocol.mainnet.json").Build());
                        Settings.Initialize(new ConfigurationBuilder().AddJsonFile("config.mainnet.json").Build());
                        break;
                }
            system = new NeoSystem();
            system.StartNode(new ChannelsConfig
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
                    Program.Wallet = OpenWallet(Settings.Default.UnlockWallet.Path, Settings.Default.UnlockWallet.Password);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"Warning: wallet file \"{Settings.Default.UnlockWallet.Path}\" not found.");
                }
                catch (CryptographicException)
                {
                    Console.WriteLine($"failed to open file \"{Settings.Default.UnlockWallet.Path}\"");
                }
                if (Settings.Default.UnlockWallet.StartConsensus && Program.Wallet != null)
                {
                    OnStartConsensusCommand(null);
                }
            }
            if (useRPC)
            {
                system.StartRpc(Settings.Default.RPC.BindAddress,
                    Settings.Default.RPC.Port,
                    wallet: Program.Wallet,
                    sslCert: Settings.Default.RPC.SslCert,
                    password: Settings.Default.RPC.SslCertPassword,
                    maxGasInvoke: Settings.Default.RPC.MaxGasInvoke);
            }
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
            if (NoWallet()) return true;
            ShowPrompt = false;
            system.StartConsensus(Program.Wallet);
            return true;
        }

        protected internal override void OnStop()
        {
            system.Dispose();
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

        private bool OnInstallCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("error");
                return true;
            }

            bool isTemp;
            string fileName;
            var pluginName = args[1];

            if (!File.Exists(pluginName))
            {
                if (string.IsNullOrEmpty(Settings.Default.PluginURL))
                {
                    Console.WriteLine("You must define `PluginURL` in your `config.json`");
                    return true;
                }

                var address = string.Format(Settings.Default.PluginURL, pluginName, typeof(Plugin).Assembly.GetVersion());
                fileName = Path.Combine(Path.GetTempPath(), $"{pluginName}.zip");
                isTemp = true;

                Console.WriteLine($"Downloading from {address}");
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(address, fileName);
                }
            }
            else
            {
                fileName = pluginName;
                isTemp = false;
            }

            try
            {
                ZipFile.ExtractToDirectory(fileName, ".");
            }
            catch (IOException)
            {
                Console.WriteLine($"Plugin already exist.");
                return true;
            }
            finally
            {
                if (isTemp)
                {
                    File.Delete(fileName);
                }
            }

            Console.WriteLine($"Install successful, please restart neo-cli.");
            return true;
        }

        private bool OnUnInstallCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("error");
                return true;
            }

            var pluginName = args[1];

            if (!Plugin.Plugins.Any(u => u.Name == pluginName))
            {
                Console.WriteLine("Plugin not found");
                return true;
            }

            if (Directory.Exists(Path.Combine("Plugins", pluginName)))
            {
                Directory.Delete(Path.Combine("Plugins", pluginName), true);
            }

            File.Delete(Path.Combine("Plugins", $"{pluginName}.dll"));
            Console.WriteLine($"Uninstall successful, please restart neo-cli.");
            return true;
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
            string password = ReadUserInput("password", true);
            if (password.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }
            string path_new = Path.ChangeExtension(path, ".json");
            if (File.Exists(path_new))
            {
                Console.WriteLine($"File '{path_new}' already exists");
                return true;
            }
            NEP6Wallet.Migrate(path_new, path, password).Save();
            Console.WriteLine($"Wallet file upgrade complete. New wallet file has been auto-saved at: {path_new}");
            return true;
        }

        private static Wallet OpenWallet(string path, string password)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException();
            }

            if (Path.GetExtension(path) == ".db3")
            {
                return UserWallet.Open(path, password);
            }
            else
            {
                NEP6Wallet nep6wallet = new NEP6Wallet(path);
                nep6wallet.Unlock(password);
                return nep6wallet;
            }
        }

        private static void WriteLineWithoutFlicker(string message = "", int maxWidth = 80)
        {
            if (message.Length > 0) Console.Write(message);
            var spacesToErase = maxWidth - message.Length;
            if (spacesToErase < 0) spacesToErase = 0;
            Console.WriteLine(new string(' ', spacesToErase));
        }
    }
}
