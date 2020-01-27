using Neo.CLI.CommandParser;
using Neo.Cryptography.ECC;
using Neo.IO.Json;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private readonly Dictionary<Type, Func<List<CommandToken>, bool, object>> _handlers = new Dictionary<Type, Func<List<CommandToken>, bool, object>>();

        class SelectedCommand
        {
            public ConsoleCommandAttribute Command { get; set; }
            public object[] Arguments { get; set; }
        }

        protected virtual bool OnCommand(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine))
            {
                return true;
            }

            var tokens = CommandToken.Parse(commandLine).ToArray();
            var availableCommands = new List<SelectedCommand>();

            foreach (var entries in _verbs.Values)
            {
                foreach (var command in entries)
                {
                    if (command.IsThisCommand(tokens, out var consumedTokens))
                    {
                        var arguments = new List<object>();
                        var args = new List<CommandToken>(tokens.Skip(consumedTokens));

                        try
                        {
                            foreach (var arg in command.Method.GetParameters())
                            {
                                // Trim start

                                while (args.FirstOrDefault() is CommandSpaceToken) args.RemoveAt(0);

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
                                        // Some arguments are wrong, we must show the help

                                        return OnCommand($"help \"{ command.Key}\"");
                                    }
                                }
                            }

                            availableCommands.Add(new SelectedCommand()
                            {
                                Arguments = arguments.ToArray(),
                                Command = command,
                            });
                        }
                        catch
                        {
                            // Skip parse errors
                        }
                    }
                }
            }

            switch (availableCommands.Count)
            {
                case 0: return false;
                case 1:
                    {
                        var command = availableCommands[0];
                        command.Command.Method.Invoke(command.Command.Instance, command.Arguments);
                        return true;
                    }
                default:
                    {
                        var command = availableCommands[0];
                        command.Command.Method.Invoke(command.Command.Instance, command.Arguments);
                        return true;

                        // throw new ArgumentException("Ambiguous calls for: " + string.Join(',', availableCommands.Select(u => u.Command.Key).Distinct()));
                    }
            }
        }

        private bool TryProcessValue(Type parameterType, List<CommandToken> args, bool canConsumeAll, out object value)
        {
            if (args.Count > 0 && _handlers.TryGetValue(parameterType, out var handler))
            {
                value = handler(args, canConsumeAll);
                return true;
            }

            if (args.Count > 0 && parameterType.IsEnum)
            {
                // Default conversion for enums

                var token = args[0];
                args.RemoveAt(0);

                value = Enum.Parse(parameterType, token.Value, true);
                return true;
            }

            value = null;
            return false;
        }

        #region Commands

        /// <summary>
        /// Process "help" command
        /// </summary>
        [ConsoleCommand("help", HelpCategory = "Normal Commands")]
        protected void OnHelpCommand([CaptureWholeArgument] string key)
        {
            var withHelp = new List<ConsoleCommandAttribute>();

            foreach (var commands in _verbs.Values.Select(u => u))
            {
                withHelp.AddRange(commands.Where(u => !string.IsNullOrEmpty(u.HelpCategory)));
            }

            withHelp.Sort((a, b) =>
            {
                var cate = a.HelpCategory.CompareTo(b.HelpCategory);
                if (cate == 0)
                {
                    cate = a.Key.CompareTo(b.Key);
                }
                return cate;
            });

            if (string.IsNullOrEmpty(key))
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
                        .Select(u => u.HasDefaultValue ? $"[{u.Name}={u.DefaultValue.ToString()}]" : $"<{u.Name}>"))
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
                        .Select(u => u.HasDefaultValue ? $"[{u.Name}={u.DefaultValue.ToString()}]" : $"<{u.Name}>"))
                        );
                }

                if (!found)
                {
                    throw new ArgumentException($"Command not found.");
                }
            }
        }

        /// <summary>
        /// Process "help" command
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
                    var str = CommandToken.ToString(args);
                    args.Clear();
                    return str;
                }

                var token = args[0];
                args.RemoveAt(0);

                return token.Value;
            });

            RegisterCommandHander(typeof(byte), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, false);
                return byte.Parse(str);
            });

            RegisterCommandHander(typeof(bool), (args, canConsumeAll) =>
            {
                var str = ((string)_handlers[typeof(bool)](args, false)).ToLowerInvariant();
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
                return UInt160.Parse(str);
            });

            RegisterCommandHander(typeof(UInt256), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, false);
                return UInt256.Parse(str);
            });

            RegisterCommandHander(typeof(UInt256[]), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, canConsumeAll);
                return str.Split(',', ' ').Select(u => UInt256.Parse(u)).ToArray();
            });

            RegisterCommandHander(typeof(ECPoint[]), (args, canConsumeAll) =>
            {
                var str = (string)_handlers[typeof(string)](args, canConsumeAll);
                return str.Split(',', ' ').Select(u => ECPoint.Parse(str, ECCurve.Secp256r1)).ToArray();
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

            RegisterCommand(this);
        }

        /// <summary>
        /// Register command handler
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="handler">Handler</param>
        public void RegisterCommandHander(Type type, Func<List<CommandToken>, bool, object> handler)
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
