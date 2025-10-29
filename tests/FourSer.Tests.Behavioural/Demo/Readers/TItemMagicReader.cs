using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TItemMagic.tcd")]
    [GenerateSerializer]
    public partial class TItemMagicCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItemMagicRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TItemMagicRecord
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public ushort Id { get; set; }
        public byte OptionKind { get; set; }
        public float Utility { get; set; }
        public byte SoundEffect { get; set; }
    }
}
