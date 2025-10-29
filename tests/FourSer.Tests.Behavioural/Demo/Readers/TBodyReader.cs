using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TBody.tcd")]
    [GenerateSerializer]
    public partial class TBodyAppearanceCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TBodyAppearance> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TBodyAppearance
    {
        public byte BodyId { get; set; }
        public TBodyClothAppearance Appearance { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TBodyClothAppearance
    {
        public uint Clk { get; set; }
        public uint Cl { get; set; }
        public uint Mesh { get; set; }
        public byte HideSlotId { get; set; }
        public byte HidePartId { get; set; }
        public byte HideRaceId { get; set; }
    }
}
