using Neo.IO.Json;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;
using Boolean = Neo.VM.Types.Boolean;
using Buffer = Neo.VM.Types.Buffer;

namespace Neo.CLI
{
    internal static class Helper
    {
        public static bool ToBool(this string input)
        {
            if (input == null) return false;

            input = input.ToLowerInvariant();

            return input == "true" || input == "yes" || input == "1";
        }

        public static bool IsYes(this string input)
        {
            if (input == null) return false;

            input = input.ToLowerInvariant();

            return input == "yes" || input == "y";
        }
        public static JObject ToJson(this StackItem item)
        {
            return ToJson(item, null);
        }

        private static JObject ToJson(StackItem item, HashSet<StackItem> context)
        {
            JObject json = new JObject();
            json["type"] = item.Type;
            switch (item)
            {
                case Array array:
                    context ??= new HashSet<StackItem>(ReferenceEqualityComparer.Default);
                    if (!context.Add(array)) throw new InvalidOperationException();
                    json["value"] = new JArray(array.Select(p => ToJson(p, context)));
                    break;
                case Boolean boolean:
                    json["value"] = boolean.ToBoolean();
                    break;
                case Buffer buffer:
                    json["value"] = Convert.ToBase64String(buffer.InnerBuffer);
                    break;
                case ByteString byteString:
                    json["value"] = Convert.ToBase64String(byteString.Span);
                    break;
                case Integer integer:
                    json["value"] = integer.ToBigInteger().ToString();
                    break;
                case Map map:
                    context ??= new HashSet<StackItem>(ReferenceEqualityComparer.Default);
                    if (!context.Add(map)) throw new InvalidOperationException();
                    json["value"] = new JArray(map.Select(p =>
                    {
                        JObject item = new JObject();
                        item["key"] = ToJson(p.Key, context);
                        item["value"] = ToJson(p.Value, context);
                        return item;
                    }));
                    break;
                case Pointer pointer:
                    json["value"] = pointer.Position;
                    break;
            }
            return json;
        }
    }
}
