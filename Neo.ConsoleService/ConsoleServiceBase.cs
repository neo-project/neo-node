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

namespace Neo.ConsoleService
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
        private readonly Dictionary<string, List<ConsoleCommandMethod>> _verbs = new Dictionary<string, List<ConsoleCommandMethod>>();
        private readonly Dictionary<string, object> _instances = new Dictionary<string, object>();
        private readonly Dictionary<Type, Func<List<CommandToken>, bool, object>> _handlers = new Dictionary<Type, Func<List<CommandToken>, bool, object>>();

        private bool OnCommand(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine))
            {
                return true;
            }

            string possibleHelp = null;
            var commandArgs = CommandToken.Parse(commandLine).ToArray();
            var availableCommands = new List<(ConsoleCommandMethod Command, object[] Arguments)>();

            foreach (var entries in _verbs.Values)
            {
                foreach (var command in entries)
                {
                    if (command.IsThisCommand(commandArgs, out var consumedArgs))
                    {
                        var arguments = new List<object>();
                        var args = commandArgs.Skip(consumedArgs).ToList();

                        CommandSpaceToken.Trim(args);

                        try
                        {
                            var parameters = command.Method.GetParameters();

                            foreach (var arg in parameters)
                            {
                                // Parse argument

                                if (TryProcessValue(arg.ParameterType, args, arg == parameters.Last(), out var value))
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
                        // Show Ambiguous call

                        throw new ArgumentException("Ambiguous calls for: " + string.Join(',', availableCommands.Select(u => u.Command.Key).Distinct()));
                    }
            }
        }

        private bool TryProcessValue(Type parameterType, List<CommandToken> args, bool canConsumeAll, out object value)
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
                    var arg = CommandToken.ReadString(args, canConsumeAll);
                    value = Enum.Parse(parameterType, arg.Trim(), true);
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
        [ConsoleCommand("help", Category = "Base Commands")]
        protected void OnHelpCommand(string key)
        {
            GetAllCommands(key, out List<ConsoleCommandMethod> withHelp);

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
        [ConsoleCommand("clear", Category = "Base Commands", Description = "Clear is used in order to clean the console output.")]
        protected void OnClear()
        {
            Console.Clear();
        }

        /// <summary>
        /// Process "version" command
        /// </summary>
        [ConsoleCommand("version", Category = "Base Commands", Description = "Show the current version.")]
        protected void OnVersion()
        {
            Console.WriteLine(Assembly.GetEntryAssembly().GetName().Version);
        }

        /// <summary>
        /// Process "exit" command
        /// </summary>
        [ConsoleCommand("exit", Category = "Base Commands", Description = "Exit the node.")]
        protected void OnExit()
        {
            _running = false;
        }

        #endregion

        public virtual void OnStart(string[] args)
        {
            // Register sigterm event handler
            AssemblyLoadContext.Default.Unloading += SigTermEventHandler;
            // Register sigint event handler
            Console.CancelKeyPress += CancelHandler;
        }

        public virtual void OnStop()
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

        private void GetAllCommands(string key, out List<ConsoleCommandMethod> withHelp)
        {
            withHelp = new List<ConsoleCommandMethod>();
            // Try to find a plugin with this name
            if (_instances.TryGetValue(key.Trim().ToLowerInvariant(), out var instance))
            {
                // Filter only the help of this plugin
                key = "";
                foreach (var commands in _verbs.Values.Select(u => u))
                {
                    withHelp.AddRange
                        (
                        commands.Where(u => !string.IsNullOrEmpty(u.HelpCategory) && u.Instance == instance)
                        );
                }
            }
            else
            {
                // Fetch commands
                foreach (var commands in _verbs.Values.Select(u => u))
                {
                    withHelp.AddRange(commands.Where(u => !string.IsNullOrEmpty(u.HelpCategory)));
                }
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

            RegisterCommandHander<string>((args, canConsumeAll) =>
            {
                return CommandToken.ReadString(args, canConsumeAll);
            });

            RegisterCommandHander<string[]>((args, canConsumeAll) =>
            {
                if (canConsumeAll)
                {
                    var ret = CommandToken.ToString(args);
                    args.Clear();
                    return ret.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    return CommandToken.ReadString(args, false).Split(',', ' ');
                }
            });

            RegisterCommandHander<string, byte>(false, (str) => byte.Parse(str));
            RegisterCommandHander<string, bool>(false, (str) => str == "1" || str == "yes" || str == "y" || bool.Parse(str));
            RegisterCommandHander<string, ushort>(false, (str) => ushort.Parse(str));
            RegisterCommandHander<string, uint>(false, (str) => uint.Parse(str));
            RegisterCommandHander<string, IPAddress>(false, (str) => IPAddress.Parse(str));
        }

        /// <summary>
        /// Register command handler
        /// </summary>
        /// <typeparam name="TRet">Return type</typeparam>
        /// <param name="handler">Handler</param>
        private void RegisterCommandHander<TRet>(Func<List<CommandToken>, bool, object> handler)
        {
            _handlers[typeof(TRet)] = handler;
        }

        /// <summary>
        /// Register command handler
        /// </summary>
        /// <typeparam name="T">Base type</typeparam>
        /// <typeparam name="TRet">Return type</typeparam>
        /// <param name="canConsumeAll">Can consume all</param>
        /// <param name="handler">Handler</param>
        public void RegisterCommandHander<T, TRet>(bool canConsumeAll, Func<T, object> handler)
        {
            _handlers[typeof(TRet)] = (args, cosumeAll) =>
            {
                var value = (T)_handlers[typeof(T)](args, canConsumeAll);
                return handler(value);
            };
        }

        /// <summary>
        /// Register command handler
        /// </summary>
        /// <typeparam name="T">Base type</typeparam>
        /// <typeparam name="TRet">Return type</typeparam>
        /// <param name="handler">Handler</param>
        public void RegisterCommandHander<T, TRet>(Func<T, object> handler)
        {
            _handlers[typeof(TRet)] = (args, cosumeAll) =>
            {
                var value = (T)_handlers[typeof(T)](args, cosumeAll);
                return handler(value);
            };
        }

        /// <summary>
        /// Register commands
        /// </summary>
        /// <param name="instance">Instance</param>
        /// <param name="name">Name</param>
        public void RegisterCommand(object instance, string name = null)
        {
            if (!string.IsNullOrEmpty(name))
            {
                _instances.Add(name.ToLowerInvariant(), instance);
            }

            foreach (var method in instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var attribute in method.GetCustomAttributes<ConsoleCommandAttribute>())
                {
                    // Check handlers

                    if (!method.GetParameters().All(u => u.ParameterType.IsEnum || _handlers.ContainsKey(u.ParameterType)))
                    {
                        throw new ArgumentException("Handler not found for the command: " + method.ToString());
                    }

                    // Add command

                    var command = new ConsoleCommandMethod(instance, method, attribute);

                    if (!_verbs.TryGetValue(command.Key, out var commands))
                    {
                        _verbs.Add(command.Key, new List<ConsoleCommandMethod>(new[] { command }));
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
                Debug.Assert(OperatingSystem.IsWindows());
                ServiceBase.Run(new ServiceProxy(this));
            }
        }


        protected string ReadLine()
        {
            Task<string> readLineTask = Task.Run(() =>
            {
                return Console.ReadLine();
            });

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

        public virtual void RunConsole()
        {
            _running = true;

            var consoleAutofill = new ConsoleAutofill();
            GetAllCommands("", out List<ConsoleCommandMethod> withHelp);
            List<string> commands = withHelp.Select(p => p.Key).ToList();

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                try
                {
                    Console.Title = ServiceName;
                }
                catch { }

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.SetIn(new StreamReader(Console.OpenStandardInput(), Console.InputEncoding, false, ushort.MaxValue));

            while (_running)
            {
                if (ShowPrompt)
                {
                    PrintPrompt(Prompt);
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                string line;//= ReadLine()?.Trim();
                var builder = new StringBuilder();
                var input = Console.ReadKey(intercept: true);
                while (input.Key != ConsoleKey.Enter)
                {
                    var currentInput = builder.ToString();
                    if (input.Key == ConsoleKey.Tab)
                    {
                        var match =
                            consoleAutofill.Autofill(currentInput,
                                commands, true);

                        if (string.IsNullOrEmpty(match))
                        {
                            input = Console.ReadKey(intercept: true);
                            continue;
                        }

                        ClearCurrentLine(Prompt);
                        builder.Clear();

                        Console.Write(match);
                        builder.Append(match);
                    }
                    else
                    {
                        if (input.Key == ConsoleKey.Backspace && currentInput.Length > 0)
                        {
                            builder.Remove(builder.Length - 1, 1);
                            ClearCurrentLine(Prompt);

                            currentInput = currentInput.Remove(currentInput.Length - 1);
                            Console.Write(currentInput);
                        }
                        else
                        {
                            var key = input.KeyChar;
                            builder.Append(key);
                            Console.Write(key);
                        }
                    }
                    input = Console.ReadKey(intercept: true);
                }
                line = builder.ToString();
                Console.WriteLine();
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

        private static void ClearCurrentLine(string Prompt)
        {
            var currentLine = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLine);
            PrintPrompt(Prompt);
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        private static void PrintPrompt(string Prompt)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{Prompt}> ");
        }
    }
}
