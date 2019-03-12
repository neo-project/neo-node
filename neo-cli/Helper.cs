using System.Linq;
using System.Reflection;

namespace Neo
{
    internal static class Helper
    {
        internal static bool ToBool(this string input)
        {
            input = input.ToLowerInvariant();

            return input == "true" || input == "yes" || input == "1";
        }

        internal static string GetVersion(this Assembly assembly)
        {
            CustomAttributeData attribute = assembly.CustomAttributes.FirstOrDefault(p => p.AttributeType == typeof(AssemblyInformationalVersionAttribute));
            if (attribute == null) return assembly.GetName().Version.ToString(3);
            return (string)attribute.ConstructorArguments[0].Value;
        }
    }
}
