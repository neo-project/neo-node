using System;
using System.Diagnostics;

namespace Neo.CLI.CommandParser
{
    [DebuggerDisplay("Value={Value}, RequireQuotes={RequireQuotes}")]
    public class CommandStringToken : CommandToken
    {
        /// <summary>
        /// Require quotes
        /// </summary>
        public bool RequireQuotes { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="requireQuotes">Require quotes</param>
        public CommandStringToken(string value, bool requireQuotes = false) : base(CommandTokenType.String)
        {
            Value = value;
            RequireQuotes = requireQuotes || value.Contains("\"");
        }

        /// <summary>
        /// Parse command line spaces
        /// </summary>
        /// <param name="commandLine">Command line</param>
        /// <param name="index">Index</param>
        /// <returns>CommandSpaceToken</returns>
        internal static CommandStringToken Parse(string commandLine, ref int index)
        {
            int end;
            bool startWithQuotes = commandLine[index] == '\"';

            if (startWithQuotes)
            {
                var ix = index++;

                do
                {
                    end = commandLine.IndexOf('\"', ix);

                    if (end == -1)
                    {
                        throw new ArgumentException("String not closed");
                    }

                    if (IsScaped(commandLine, end))
                    {
                        ix = end + 1;
                        end = -1;
                    }
                    else
                    {
                        //count -= index;
                    }
                }
                while (end < 0);
            }
            else
            {
                end = commandLine.IndexOf(' ', index + 1);
            }

            if (end == -1)
            {
                end = commandLine.Length;
            }

            var ret = new CommandStringToken(commandLine.Substring(index, end - index), startWithQuotes);
            index += end - index;
            if (startWithQuotes) index++;
            return ret;
        }

        private static bool IsScaped(string commandLine, int index)
        {
            while (index >= 0)
            {
                if (commandLine[index] != '\\') return false;
                index--;
            }

            return true;
        }
    }
}
