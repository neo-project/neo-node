using System;
using System.Diagnostics;
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
            Verbs = verbs;
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
        /// <param name="consumedTokens">Consumed tokens</param>
        /// <returns>True if is this command</returns>
        public bool IsThisCommand(CommandToken[] tokens, out int consumedTokens)
        {
            var verbsFound = 0;
            var tokenIndex = 0;
            consumedTokens = 0;

            for (int x = 0; x < tokens.Length; x++)
            {
                consumedTokens++;

                switch (tokens[x])
                {
                    case CommandStringToken str:
                        {
                            if (tokenIndex < Verbs.Length &&
                                str.Value.Equals(Verbs[tokenIndex], StringComparison.InvariantCultureIgnoreCase))
                            {
                                verbsFound++;
                                tokenIndex++;

                                if (verbsFound == Verbs.Length)
                                {
                                    for (int i = x + 1; i < tokens.Length; i++)
                                    {
                                        if (tokens[i] is CommandSpaceToken)
                                        {
                                            consumedTokens++;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }

                                    return true;
                                }
                            }
                            else
                            {
                                return false;
                            }

                            break;
                        }
                }
            }

            return false;
        }
    }
}
