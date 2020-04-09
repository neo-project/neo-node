using System;
using System.Diagnostics;

namespace Neo.ConsoleService
{
    [DebuggerDisplay("Value={Value}, Value={Value}")]
    internal class CommandQuoteToken : CommandToken
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="offset">Offset</param>
        /// <param name="value">Value</param>
        public CommandQuoteToken(int offset, char value) : base(CommandTokenType.Quote, offset)
        {
            if (value != '\'' && value != '"')
            {
                throw new ArgumentException("Not valid quote");
            }

            Value = value.ToString();
        }

        /// <summary>
        /// Parse command line quotes
        /// </summary>
        /// <param name="commandLine">Command line</param>
        /// <param name="index">Index</param>
        /// <returns>CommandQuoteToken</returns>
        internal static CommandQuoteToken Parse(string commandLine, ref int index)
        {
            var c = commandLine[index];

            if (c == '\'' || c == '"')
            {
                index++;
                return new CommandQuoteToken(index - 1, c);
            }

            throw new ArgumentException("No quote found");
        }
    }
}
