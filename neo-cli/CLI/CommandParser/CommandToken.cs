using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.CLI.CommandParser
{
    public abstract class CommandToken
    {
        /// <summary>
        /// Type
        /// </summary>
        public CommandTokenType Type { get; }

        /// <summary>
        /// Value
        /// </summary>
        public string Value { get; protected set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Type</param>
        protected CommandToken(CommandTokenType type)
        {
            Type = type;
        }

        /// <summary>
        /// Parse command line
        /// </summary>
        /// <param name="commandLine">Command line</param>
        /// <returns></returns>
        public static IEnumerable<CommandToken> Parse(string commandLine)
        {
            for (int x = 0, count = commandLine.Length; x < count;)
            {
                switch (commandLine[x])
                {
                    case ' ':
                        {
                            yield return CommandSpaceToken.Parse(commandLine, ref x);
                            break;
                        }
                    default:
                        {
                            yield return CommandStringToken.Parse(commandLine, ref x);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Create a string from token list
        /// </summary>
        /// <param name="tokens">Tokens</param>
        /// <returns>String</returns>
        public static string ToString(IEnumerable<CommandToken> tokens)
        {
            var sb = new StringBuilder();

            foreach (var token in tokens)
            {
                sb.Append(token.Value);
            }

            return sb.ToString();
        }
    }
}
