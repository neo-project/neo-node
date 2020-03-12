using System;
using System.Diagnostics;
using System.Linq;

namespace Neo.ConsoleService
{
    [DebuggerDisplay("Verbs={string.Join(' ',Verbs)}")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ConsoleCommandAttribute : Attribute
    {
        /// <summary>
        /// Verbs
        /// </summary>
        public string[] Verbs { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="verbs">Verbs</param>
        public ConsoleCommandAttribute(params string[] verbs)
        {
            Verbs = verbs.Select(u => u.ToLowerInvariant()).ToArray();
        }
    }
}
