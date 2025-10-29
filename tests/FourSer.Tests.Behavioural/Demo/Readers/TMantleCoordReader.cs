using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMantleCoord.tcd")]
    [GenerateSerializer]
    public partial class TMantleCoordCatalog
    {
        public uint Version { get; set; }

        [SerializeCollection(CountType = typeof(ushort))]
        public List<TMantleCoordEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TMantleCoordEntry
    {
        public byte RaceId { get; set; }
        public byte SexId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string MeshName { get; set; } = string.Empty;

        [SerializeCollection(CountType = typeof(uint))]
        public List<MantleUvCoordinate> Uv1 { get; set; } = new();

        [SerializeCollection(CountType = typeof(uint))]
        public List<MantleUvCoordinate> Uv2 { get; set; } = new();

        [SerializeCollection(CountType = typeof(uint))]
        public List<MantleUvCoordinate> Uv3 { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class MantleUvCoordinate
    {
        public float LeftTopX { get; set; }
        public float LeftTopY { get; set; }
        public float RightTopX { get; set; }
        public float RightTopY { get; set; }
        public float LeftBottomX { get; set; }
        public float LeftBottomY { get; set; }
        public float RightBottomX { get; set; }
        public float RightBottomY { get; set; }
    }
}
