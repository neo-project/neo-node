using Neo.IO.Json;
using Neo.SmartContract;
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

        private static JObject ToJson(StackItem item, List<(StackItem, JObject)> context = null)
        {
            if (item is null) throw new ArgumentNullException();
            JObject parameter = null;
            switch (item)
            {
                case Array array:
                    if (context is null)
                        context = new List<(StackItem, JObject)>();
                    else
                        (_, parameter) = context.FirstOrDefault(p => ReferenceEquals(p.Item1, item));
                    if (parameter is null)
                    {
                        parameter = new JObject();
                        parameter["type"] = ContractParameterType.Array;
                        context.Add((item, parameter));
                        parameter["value"] = array.Select(p => ToJson(p, context)).ToArray();
                    }
                    break;
                case Map map:
                    if (context is null)
                        context = new List<(StackItem, JObject)>();
                    else
                        (_, parameter) = context.FirstOrDefault(p => ReferenceEquals(p.Item1, item));
                    if (parameter is null)
                    {
                        parameter = new JObject();
                        parameter["type"] = ContractParameterType.Map;
                        context.Add((item, parameter));
                        parameter["value"] = map.Select(p =>
                        {
                            JObject item = new JObject();
                            item["key"] = ToJson(p.Key, context);
                            item["value"] = ToJson(p.Value, context);
                            return item;
                        }).ToArray();
                    }
                    break;
                case Boolean _:
                    parameter = new JObject();
                    parameter["type"] = ContractParameterType.Boolean;
                    parameter["value"] = item.ToBoolean();
                    break;
                case ByteString byteString:
                    parameter = new JObject();
                    parameter["type"] = ContractParameterType.ByteArray;
                    parameter["value"] = Convert.ToBase64String(((ByteString)item).Span.ToArray());
                    break;
                case Buffer buffer:
                    parameter = new JObject();
                    parameter["type"] = ContractParameterType.ByteArray;
                    parameter["value"] = Convert.ToBase64String(((Buffer)item).InnerBuffer.ToArray());
                    break;
                case Integer i:
                    parameter = new JObject();
                    parameter["type"] = ContractParameterType.Integer;
                    parameter["value"] = i.ToBigInteger().ToString();
                    break;
                case Null _:
                    parameter = new JObject();
                    parameter["type"] = ContractParameterType.Any;
                    parameter["value"] = null;
                    break;
                default:
                    throw new ArgumentException("Not supported to Json.");
            }
            return parameter;
        }
    }
}
