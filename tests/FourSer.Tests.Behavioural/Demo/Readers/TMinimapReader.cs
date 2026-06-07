using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMinimap.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TMinimapCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TMinimapEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TMinimapEntry
    {
        public uint UnitId { get; set; }
        public uint TextureId { get; set; }
        public float Scale { get; set; }
        public float PositionX { get; set; }
        public float PositionZ { get; set; }
        public byte WorldId { get; set; }
        public uint WorldButtonId { get; set; }
        public byte WorldLevel { get; set; }
    }
}

