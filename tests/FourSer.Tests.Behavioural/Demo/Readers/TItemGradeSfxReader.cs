using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TItemGradeSfx.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TItemGradeSfxCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItemGradeSfxRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TItemGradeSfxRecord
    {
        public ushort Id { get; set; }

        [SerializeCollection(CountSize = 4)]
        public ushort[] Effects { get; set; } = new ushort[4];
    }
}
