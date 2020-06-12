using Neo.Network.P2P.Payloads;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;

namespace Neo.GUI.Wrappers
{
    internal class TransactionWrapper
    {
        [Category("Basic")]
        public byte Version { get; set; }
        [Category("Basic")]
        public uint Nonce { get; set; }
        [Category("Basic")]
        [TypeConverter(typeof(UIntBaseConverter))]
        public UInt160 Sender { get; set; }
        [Category("Basic")]
        public long SystemFee { get; set; }
        [Category("Basic")]
        public long NetworkFee { get; set; }
        [Category("Basic")]
        public uint ValidUntilBlock { get; set; }
        [Category("Basic")]
        public List<TransactionAttributeWrapper> Attributes { get; set; } = new List<TransactionAttributeWrapper>();
        [Category("Basic")]
        [Editor(typeof(ScriptEditor), typeof(UITypeEditor))]
        [TypeConverter(typeof(HexConverter))]
        public byte[] Script { get; set; }
        [Category("Basic")]
        public List<WitnessWrapper> Witnesses { get; set; } = new List<WitnessWrapper>();

        public Transaction Unwrap()
        {
            return new Transaction
            {
                Version = Version,
                Nonce = Nonce,
                Sender = Sender,
                SystemFee = SystemFee,
                NetworkFee = NetworkFee,
                ValidUntilBlock = ValidUntilBlock,
                Attributes = Attributes.Select(p => p.Unwrap()).ToArray(),
                Script = Script,
                Witnesses = Witnesses.Select(p => p.Unwrap()).ToArray()
            };
        }
    }
}
