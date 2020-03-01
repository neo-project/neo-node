using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Neo.CommandParser
{
    [DebuggerDisplay("Key={Key}")]
    internal class ConsoleCommandMethod
    {
        /// <summary>
        /// Verbs
        /// </summary>
        public string[] Verbs { get; }

        /// <summary>
        /// Key
        /// </summary>
        public string Key => string.Join(' ', Verbs);

        /// <summary>
        /// Help category
        /// </summary>
        public string HelpCategory { get; set; }

        /// <summary>
        /// Help message
        /// </summary>
        public string HelpMessage { get; set; }

        /// <summary>
        /// Instance
        /// </summary>
        public object Instance { get; }

        /// <summary>
        /// Method
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// Set instance command
        /// </summary>
        /// <param name="instance">Instance</param>
        /// <param name="method">Method</param>
        public ConsoleCommandMethod(object instance, MethodInfo method)
        {
            Method = method;
            Instance = instance;

            var command = method.GetCustomAttribute<ConsoleCommandAttribute>();
            if (command != null)
            {
                Verbs = command.Verbs;
            }

            var category = method.GetCustomAttribute<CategoryAttribute>();
            if (category != null)
            {
                HelpCategory = category.Category;
            }

            var desc = method.GetCustomAttribute<DescriptionAttribute>();
            if (desc != null)
            {
                HelpMessage = desc.Description;
            }
        }

        /// <summary>
        /// Is this command
        /// </summary>
        /// <param name="tokens">Tokens</param>
        /// <param name="consumedArgs">Consumed Arguments</param>
        /// <returns>True if is this command</returns>
        public bool IsThisCommand(string[] tokens, out int consumedArgs)
        {
            consumedArgs = Verbs.Length;
            return Verbs.SequenceEqual(tokens.Take(consumedArgs).Select(u => u.ToLowerInvariant()));
        }
    }
}
