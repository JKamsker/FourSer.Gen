using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("THand.tcd")]
    [GenerateSerializer]
    public partial class THandAppearanceCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<THandAppearance> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class THandAppearance
    {
        public byte HandId { get; set; }
        public THandClothAppearance Appearance { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class THandClothAppearance
    {
        public uint Clk { get; set; }
        public uint Cl { get; set; }
        public uint Mesh { get; set; }
        public byte HideSlotId { get; set; }
        public byte HidePartId { get; set; }
        public byte HideRaceId { get; set; }
    }
}
