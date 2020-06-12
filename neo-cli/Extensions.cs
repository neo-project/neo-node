using System.Linq;
using System.Reflection;

namespace Neo
{
    /// <summary>
    /// Extension methods
    /// </summary>
    internal static class Extensions
    {
        public static string GetVersion(this Assembly assembly)
        {
            CustomAttributeData attribute = assembly.CustomAttributes.FirstOrDefault(p => p.AttributeType == typeof(AssemblyInformationalVersionAttribute));
            if (attribute == null) return assembly.GetName().Version.ToString(3);
            return (string)attribute.ConstructorArguments[0].Value;
        }
    }
}
