using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TItemMagicSfx.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TItemMagicSfxCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItemMagicSfxRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TItemMagicSfxRecord
    {
        public ushort Id { get; set; }

        [SerializeCollection(CountSize = 15)]
        public ushort[] Effects { get; set; } = new ushort[15];
    }
}
