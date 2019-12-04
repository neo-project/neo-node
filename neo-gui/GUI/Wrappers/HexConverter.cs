using System;
using System.ComponentModel;
using System.Globalization;

namespace Neo.GUI.Wrappers
{
    internal class HexConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
                return true;
            return false;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
                return true;
            return false;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
                return s.HexToBytes();
            throw new NotSupportedException();
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType != typeof(string))
                throw new NotSupportedException();
            if (!(value is byte[] array)) return null;
            return array.ToHexString();
        }
    }
}
