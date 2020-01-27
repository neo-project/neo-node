using System;

namespace Neo.CLI.CommandParser
{
    /// <summary>
    /// It's required if we don't require to use quotes for capture the argument.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class CaptureWholeArgumentAttribute : Attribute { }
}
