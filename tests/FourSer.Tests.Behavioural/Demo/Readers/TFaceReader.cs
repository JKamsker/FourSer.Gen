using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TFace.tcd")]
    [GenerateSerializer]
    public partial class TFaceAppearanceCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TFaceAppearance> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TFaceAppearance
    {
        public byte FaceId { get; set; }
        public byte HairId { get; set; }
        public TFaceClothAppearance Appearance { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TFaceClothAppearance
    {
        public uint Clk { get; set; }
        public uint Cl { get; set; }
        public uint Mesh { get; set; }
        public byte HideSlotId { get; set; }
        public byte HidePartId { get; set; }
        public byte HideRaceId { get; set; }
    }
}
