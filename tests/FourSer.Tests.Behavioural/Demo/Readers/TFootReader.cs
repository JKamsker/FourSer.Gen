using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TFoot.tcd")]
    [GenerateSerializer]
    public partial class TFootAppearanceCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TFootAppearance> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TFootAppearance
    {
        public byte FootId { get; set; }
        public TFootClothAppearance Appearance { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TFootClothAppearance
    {
        public uint Clk { get; set; }
        public uint Cl { get; set; }
        public uint Mesh { get; set; }
        public byte HideSlotId { get; set; }
        public byte HidePartId { get; set; }
        public byte HideRaceId { get; set; }
    }
}
