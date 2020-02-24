using Neo.CommandParser;
using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Services
{
    public abstract class ConsoleServiceBase
    {
        protected virtual string Depends => null;
        protected virtual string Prompt => "service";

        public abstract string ServiceName { get; }

        protected bool ShowPrompt { get; set; } = true;
        public bool ReadingPassword { get; set; } = false;

        private bool _running;
        private readonly CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private readonly CountdownEvent _shutdownAcknowledged = new CountdownEvent(1);
        private readonly Dictionary<string, List<ConsoleCommandAttribute>> _verbs = new Dictionary<string, List<ConsoleCommandAttribute>>();
        private readonly Dictionary<Type, Func<List<string>, bool, object>> _handlers = new Dictionary<Type, Func<List<string>, bool, object>>();

        private bool OnCommand(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine))
            {
                return true;
            }

            string possibleHelp = null;
            var tokens = CommandToken.Parse(commandLine).ToArray();
            var commandArgs = CommandToken.ToArguments(tokens);
            var availableCommands = new List<(ConsoleCommandAttribute Command, object[] Arguments)>();

            foreach (var entries in _verbs.Values)
            {
                foreach (var command in entries)
                {
                    if (command.IsThisCommand(commandArgs, out var consumedArgs))
                    {
                        var arguments = new List<object>();
                        var args = commandArgs.Skip(consumedArgs).ToList();

                        try
                        {
                            foreach (var arg in command.Method.GetParameters())
                            {
                                // Parse argument

                                if (TryProcessValue(arg.ParameterType, args, arg.GetCustomAttribute<CaptureWholeArgumentAttribute>() != null, out var value))
                                {
                                    arguments.Add(value);
                                }
                                else
                                {
                                    if (arg.HasDefaultValue)
                                    {
                                        arguments.Add(arg.DefaultValue);
                                    }
                                    else
                                    {
                                        throw new ArgumentException(arg.Name);
                                    }
                                }
                            }

                            availableCommands.Add((command, arguments.ToArray()));
                        }
                        catch
                        {
                            // Skip parse errors
                            possibleHelp = command.Key;
                        }
                    }
                }
            }

            switch (availableCommands.Count)
            {
                case 0:
                    {
                        if (!string.IsNullOrEmpty(possibleHelp))
                        {
                            OnHelpCommand(possibleHelp);
                            return true;
                        }

                        return false;
                    }
                case 1:
                    {
                        var (command, arguments) = availableCommands[0];
                        command.Method.Invoke(command.Instance, arguments);
                        return true;
                    }
                default:
                    {
                        // Check if one of them must be excluded

                        var commandsWithoutExtraArgs = availableCommands
                            .Where(u => !u.Command.ExcludeIfAmbiguous).ToList();

                        if (commandsWithoutExtraArgs.Count == 1)
                        {
                            var (command, arguments) = commandsWithoutExtraArgs[0];
                            command.Method.Invoke(command.Instance, arguments);
                            return true;
                        }

                        // Show Ambiguous call

                        throw new ArgumentException("Ambiguous calls for: " + string.Join(',', availableCommands.Select(u => u.Command.Key).Distinct()));
                    }
            }
        }

        private bool TryProcessValue(Type parameterType, List<string> args, bool canConsumeAll, out object value)
        {
            if (args.Count > 0)
            {
                if (_handlers.TryGetValue(parameterType, out var handler))
                {
                    value = handler(args, canConsumeAll);
                    return true;
                }

                if (parameterType.IsEnum)
                {
                    // Default conversion for enums

                    var arg = args[0];
                    args.RemoveAt(0);

                    value = Enum.Parse(parameterType, arg, true);
                    return true;
                }
            }

            value = null;
            return false;
        }

        #region Commands

        /// <summary>
        /// Process "help" command
        /// </summary>
        [ConsoleCommand("help", HelpCategory = "Normal Commands")]
        protected void OnHelpCommand([CaptureWholeArgument] string key = "")
        {
            var withHelp = new List<ConsoleCommandAttribute>();

            // Try to find a plugin with this name

            var plugin = Plugins.Plugin.Plugins
                            .Where(u => u.Name.Equals(key.Trim(), StringComparison.InvariantCultureIgnoreCase))
                            .FirstOrDefault();

            // Fetch commands

            foreach (var commands in _verbs.Values.Select(u => u))
            {
                withHelp.AddRange(commands.Where(u => !string.IsNullOrEmpty(u.HelpCategory)));
            }

            if (plugin != null)
            {
                // Filter only the help of this plugin

                key = "";
                withHelp = withHelp
                    .Where(u => u.Instance is Plugins.Plugin pl && pl.Name == plugin.Name)
                    .ToList();
            }

            // Sort and show

            withHelp.Sort((a, b) =>
            {
                var cate = a.HelpCategory.CompareTo(b.HelpCategory);
                if (cate == 0)
                {
                    cate = a.Key.CompareTo(b.Key);
                }
                return cate;
            });

            if (string.IsNullOrEmpty(key) || key.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                string last = null;
                foreach (var command in withHelp)
                {
                    if (last != command.HelpCategory)
                    {
                        Console.WriteLine($"{command.HelpCategory}:");
                        last = command.HelpCategory;
                    }

                    Console.Write($"\t{command.Key}");
                    Console.WriteLine(" " + string.Join(' ',
                        command.Method.GetParameters()
                        .Select(u => u.HasDefaultValue ? $"[{u.Name}={(u.DefaultValue == null ? "null" : u.DefaultValue.ToString())}]" : $"<{u.Name}>"))
                        );
                }
            }
            else
            {
                // Show help for this specific command

                string last = null;
                string lastKey = null;
                bool found = false;

                foreach (var command in withHelp.Where(u => u.Key == key))
                {
                    found = true;

                    if (last != command.HelpMessage)
                    {
                        Console.WriteLine($"{command.HelpMessage}");
                        last = command.HelpMessage;
                    }

                    if (lastKey != command.Key)
                    {
                        Console.WriteLine($"You can call this command like this:");
                        lastKey = command.Key;
                    }

                    Console.Write($"\t{command.Key}");
                    Console.WriteLine(" " + string.Join(' ',
                        command.Method.GetParameters()
                        .Select(u => u.HasDefaultValue ? $"[{u.Name}={u.DefaultValue?.ToString() ?? "null"}]" : $"<{u.Name}>"))
                        );
                }

                if (!found)
                {
                    throw new ArgumentException($"Command not found.");
                }
            }
        }

        /// <summary>
        /// Process "clear" command
        /// </summary>
        [ConsoleCommand("clear", HelpCategory = "Normal Commands", HelpMessage = "Clear is used in order to clean the console output.")]
        protected void OnClear()
        {
            Console.Clear();
        }

        /// <summary>
        /// Process "version" command
        /// </summary>
        [ConsoleCommand("version", HelpCategory = "Normal Commands", HelpMessage = "Show the current version.")]
        protected void OnVersion()
        {
            Console.WriteLine(Assembly.GetEntryAssembly().GetVersion());
        }

        /// <summary>
        /// Process "exit" command
        /// </summary>
        [ConsoleCommand("exit", HelpCategory = "Normal Commands", HelpMessage = "Exit the node.")]
        protected void OnExit()
        {
            _running = false;
        }

        #endregion

        protected internal virtual void OnStart(string[] args)
        {
            // Register sigterm event handler
            AssemblyLoadContext.Default.Unloading += SigTermEventHandler;
            // Register sigint event handler
            Console.CancelKeyPress += CancelHandler;
        }

        protected internal virtual void OnStop()
        {
            _shutdownAcknowledged.Signal();
        }

        public string ReadUserInput(string prompt, bool password = false)
        {
            const string t = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            StringBuilder sb = new StringBuilder();
            ConsoleKeyInfo key;

            if (!string.IsNullOrEmpty(prompt))
            {
                Console.Write(prompt + ": ");
            }

            if (password) ReadingPassword = true;
            var prevForeground = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;

            if (Console.IsInputRedirected)
            {
                // neo-gui Console require it
                sb.Append(Console.ReadLine());
            }
            else
            {
                do
                {
                    key = Console.ReadKey(true);

                    if (t.IndexOf(key.KeyChar) != -1)
                    {
                        sb.Append(key.KeyChar);
                        if (password)
                        {
                            Console.Write('*');
                        }
                        else
                        {
                            Console.Write(key.KeyChar);
                        }
                    }
                    else if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
                    {
                        sb.Length--;
                        Console.Write("\b \b");
                    }
                } while (key.Key != ConsoleKey.Enter);
            }

            Console.ForegroundColor = prevForeground;
            if (password) ReadingPassword = false;
            Console.WriteLine();
            return sb.ToString();
        }

        public SecureString ReadSecureString(string prompt)
        {
            const string t = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            SecureString securePwd = new SecureString();
            ConsoleKeyInfo key;

            if (!string.IsNullOrEmpty(prompt))
            {
                Console.Write(prompt + ": ");
            }

            ReadingPassword = true;
            Console.ForegroundColor = ConsoleColor.Yellow;

            do
            {
                key = Console.ReadKey(true);
                if (t.IndexOf(key.KeyChar) != -1)
                {
                    securePwd.AppendChar(key.KeyChar);
                    Console.Write('*');
                }
                else if (key.Key == ConsoleKey.Backspace && securePwd.Length > 0)
                {
                    securePwd.RemoveAt(securePwd.Length - 1);
                    Console.Write(key.KeyChar);
                    Console.Write(' ');
                    Console.Write(key.KeyChar);
                }
            } while (key.Key != ConsoleKey.Enter);

            Console.ForegroundColor = ConsoleColor.White;
            ReadingPassword = false;
            Console.WriteLine();
            securePwd.MakeReadOnly();
            return securePwd;
        }

        private void TriggerGracefulShutdown()
        {
            if (!_running) return;
            _running = false;
            _shutdownTokenSource.Cancel();
            // Wait for us to have triggered shutdown.
            _shutdownAcknowledged.Wait();
        }

        private void SigTermEventHandler(AssemblyLoadContext obj)
        {
            TriggerGracefulShutdown();
        }

        private void CancelHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            TriggerGracefulShutdown();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected ConsoleServiceBase()
        {
            // Register self commands

            RegisterCommandHander(typeof(string), (args, canConsumeAll) =>
            {
                if (canConsumeAll)
                {
                    var ret = string.Join(' ', args);
                    args.Clear();
                    return ret;
                }

                var arg = args[0];
                args.RemoveAt(0);

                return arg;
            });

            RegisterCommandHander(typeof(byte), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, false);
                return byte.Parse(str);
            });

            RegisterCommandHander(typeof(bool), (args, canConsumeAll) =>
            {
                var str = ((string)_handlers[typeof(string)](args, false)).ToLowerInvariant();
                return str == "1" || str == "yes" || str == "y" || bool.Parse(str);
            });

            RegisterCommandHander(typeof(ushort), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, false);
                return ushort.Parse(str);
            });

            RegisterCommandHander(typeof(uint), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, false);
                return uint.Parse(str);
            });

            RegisterCommandHander(typeof(UInt160), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, false);

                // Try to parse as UInt160

                if (UInt160.TryParse(str, out var addr))
                {
                    return addr;
                }

                // Accept wallet format

                return str.ToScriptHash();
            });

            RegisterCommandHander(typeof(UInt256), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, false);
                return UInt256.Parse(str);
            });

            RegisterCommandHander(typeof(UInt256[]), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, true);
                return str.Split(',', ' ').Select(u => UInt256.Parse(u.Trim())).ToArray();
            });

            RegisterCommandHander(typeof(ECPoint[]), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, true);
                return str.Split(',', ' ').Select(u => ECPoint.Parse(u.Trim(), ECCurve.Secp256r1)).ToArray();
            });

            RegisterCommandHander(typeof(JObject), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, canConsumeAll);
                return JObject.Parse(str);
            });

            RegisterCommandHander(typeof(JArray), (args, canConsumeAll) =>
            {
                var obj = (JObject)_handlers[typeof(JObject)](args, canConsumeAll);
                return (JArray)obj;
            });

            RegisterCommandHander(typeof(IPAddress), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, canConsumeAll);
                return IPAddress.Parse(str);
            });

            RegisterCommand(this);
        }

        /// <summary>
        /// Register command handler
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="handler">Handler</param>
        public void RegisterCommandHander(Type type, Func<List<string>, bool, object> handler)
        {
            _handlers[type] = handler;
        }

        /// <summary>
        /// Register commands
        /// </summary>
        /// <param name="instance">Instance</param>
        public void RegisterCommand(object instance)
        {
            foreach (var method in instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var command in method.GetCustomAttributes<ConsoleCommandAttribute>())
                {
                    // Check handlers

                    if (!method.GetParameters().All(u => u.ParameterType.IsEnum || _handlers.ContainsKey(u.ParameterType)))
                    {
                        throw new ArgumentException("Handler not found for the command: " + method.ToString());
                    }

                    // Add command

                    command.SetInstance(instance, method);

                    if (!_verbs.TryGetValue(command.Key, out var commands))
                    {
                        _verbs.Add(command.Key, new List<ConsoleCommandAttribute>(new[] { command }));
                    }
                    else
                    {
                        commands.Add(command);
                    }
                }
            }
        }

        public void Run(string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Length > 0 && args[0] == "/install")
                {
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    {
                        Console.WriteLine("Only support for installing services on Windows.");
                        return;
                    }
                    string arguments = string.Format("create {0} start= auto binPath= \"{1}\"", ServiceName, Process.GetCurrentProcess().MainModule.FileName);
                    if (!string.IsNullOrEmpty(Depends))
                    {
                        arguments += string.Format(" depend= {0}", Depends);
                    }
                    Process process = Process.Start(new ProcessStartInfo
                    {
                        Arguments = arguments,
                        FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    });
                    process.WaitForExit();
                    Console.Write(process.StandardOutput.ReadToEnd());
                }
                else if (args.Length > 0 && args[0] == "/uninstall")
                {
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    {
                        Console.WriteLine("Only support for installing services on Windows.");
                        return;
                    }
                    Process process = Process.Start(new ProcessStartInfo
                    {
                        Arguments = string.Format("delete {0}", ServiceName),
                        FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    });
                    process.WaitForExit();
                    Console.Write(process.StandardOutput.ReadToEnd());
                }
                else
                {
                    OnStart(args);
                    RunConsole();
                    OnStop();
                }
            }
            else
            {
                ServiceBase.Run(new ServiceProxy(this));
            }
        }

        protected string ReadLine()
        {
            Task<string> readLineTask = Task.Run(() => Console.ReadLine());

            try
            {
                readLineTask.Wait(_shutdownTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            return readLineTask.Result;
        }

        public void RunConsole()
        {
            _running = true;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                try
                {
                    Console.Title = ServiceName;
                }
                catch { }

            Console.ForegroundColor = ConsoleColor.DarkGreen;

            var cliV = Assembly.GetAssembly(typeof(Program)).GetVersion();
            var neoV = Assembly.GetAssembly(typeof(NeoSystem)).GetVersion();
            var vmV = Assembly.GetAssembly(typeof(ExecutionEngine)).GetVersion();
            Console.WriteLine($"{ServiceName} v{cliV}  -  NEO v{neoV}  -  NEO-VM v{vmV}");
            Console.WriteLine();

            while (_running)
            {
                if (ShowPrompt)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{Prompt}> ");
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                string line = ReadLine()?.Trim();
                if (line == null) break;
                Console.ForegroundColor = ConsoleColor.White;

                try
                {
                    if (!OnCommand(line))
                    {
                        Console.WriteLine("error: Command not found");
                    }
                }
                catch (TargetInvocationException ex)
                {
                    Console.WriteLine($"error: {ex.InnerException.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"error: {ex.Message}");
                }
            }

            Console.ResetColor();
        }
    }
}
