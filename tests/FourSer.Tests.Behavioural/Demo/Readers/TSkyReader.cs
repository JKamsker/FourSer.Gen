using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSky.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TSkyCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TSkyEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TSkyEntry
    {
        public uint SkyId { get; set; }
        public uint ObjectId { get; set; }
        public uint ClockTextureId { get; set; }
        public uint ColorTextureId { get; set; }
        public uint MeshId { get; set; }
        public uint ActionId { get; set; }
        public uint AnimationId { get; set; }
    }
}

