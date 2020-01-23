using System;
using System.Diagnostics;

namespace Neo.CLI.CommandParser
{
    [DebuggerDisplay("Value={Value}, Count={Count}")]
    public class CommandSpaceToken : CommandToken
    {
        /// <summary>
        /// Count
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="count">Count</param>
        public CommandSpaceToken(int count) : base(CommandTokenType.Space)
        {
            Value = "".PadLeft(count, ' ');
            Count = count;
        }

        /// <summary>
        /// Parse command line spaces
        /// </summary>
        /// <param name="commandLine">Command line</param>
        /// <param name="index">Index</param>
        /// <returns>CommandSpaceToken</returns>
        internal static CommandSpaceToken Parse(string commandLine, ref int index)
        {
            int count = 0;

            for (int ix = index, max = commandLine.Length; ix < max; ix++)
            {
                if (commandLine[ix] == ' ')
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (count == 0) throw new ArgumentException("No spaces found");

            index += count;
            return new CommandSpaceToken(count);
        }
    }
}
