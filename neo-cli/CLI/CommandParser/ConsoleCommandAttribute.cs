using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Neo.CLI.CommandParser
{
    [DebuggerDisplay("Key={Key}")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ConsoleCommandAttribute : Attribute
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
        internal object Instance { get; private set; }

        /// <summary>
        /// Method
        /// </summary>
        internal MethodInfo Method { get; private set; }

        /// <summary>
        /// Exclude this command if it's an ambiguous call
        /// </summary>
        public bool ExcludeIfAmbiguous { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="verbs">Verbs</param>
        public ConsoleCommandAttribute(params string[] verbs)
        {
            Verbs = verbs.Select(u => u.ToLowerInvariant()).ToArray();
        }

        /// <summary>
        /// Set instance command
        /// </summary>
        /// <param name="instance">Instance</param>
        /// <param name="method">Method</param>
        internal void SetInstance(object instance, MethodInfo method)
        {
            Method = method;
            Instance = instance;
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
