using System.Collections.Generic;
using System.Text;

namespace Neo.CommandParser
{
    public abstract class CommandToken
    {
        /// <summary>
        /// Offset
        /// </summary>
        public int Offset { get; }

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
        /// <param name="offset">Offset</param>
        protected CommandToken(CommandTokenType type, int offset)
        {
            Type = type;
            Offset = offset;
        }

        /// <summary>
        /// Parse command line
        /// </summary>
        /// <param name="commandLine">Command line</param>
        /// <returns></returns>
        public static IEnumerable<CommandToken> Parse(string commandLine)
        {
            CommandToken lastToken = null;

            for (int index = 0, count = commandLine.Length; index < count;)
            {
                switch (commandLine[index])
                {
                    case ' ':
                        {
                            lastToken = CommandSpaceToken.Parse(commandLine, ref index);
                            yield return lastToken;
                            break;
                        }
                    case '"':
                    case '\'':
                        {
                            if (lastToken is CommandQuoteToken quote)
                            {
                                // "'"

                                if (quote.Value[0] != commandLine[index])
                                {
                                    goto default;
                                }
                            }

                            lastToken = CommandQuoteToken.Parse(commandLine, ref index);
                            yield return lastToken;
                            break;
                        }
                    default:
                        {
                            lastToken = CommandStringToken.Parse(commandLine, ref index,
                                lastToken is CommandQuoteToken quote ? quote : null);

                            yield return lastToken;
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Create string arguments
        /// </summary>
        /// <param name="tokens">Tokens</param>
        /// <param name="removeEscape">Remove escape</param>
        /// <returns>Arguments</returns>
        public static string[] ToArguments(IEnumerable<CommandToken> tokens, bool removeEscape = true)
        {
            var list = new List<string>();

            CommandToken lastToken = null;

            foreach (var token in tokens)
            {
                if (token is CommandStringToken str)
                {
                    if (removeEscape && lastToken is CommandQuoteToken quote)
                    {
                        // Remove escape

                        list.Add(str.Value.Replace("\\" + quote.Value, quote.Value));
                    }
                    else
                    {
                        list.Add(str.Value);
                    }
                }

                lastToken = token;
            }

            return list.ToArray();
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
