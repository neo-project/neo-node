using Neo.Network.P2P.Payloads;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace Neo.GUI.Wrappers
{
    internal class TransactionAttributeWrapper
    {
        public TransactionAttributeType Usage { get; set; }
        [TypeConverter(typeof(HexConverter))]
        public byte[] Data { get; set; }

        public TransactionAttribute Unwrap()
        {
            using var reader = new BinaryReader(new MemoryStream(Data), Encoding.UTF8, false);

            return TransactionAttribute.DeserializeFrom(reader);
        }
    }
}
