using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TPants.tcd")]
    [GenerateSerializer]
    public partial class TPantsAppearanceCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TPantsAppearance> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TPantsAppearance
    {
        public byte PantsId { get; set; }
        public TPantsClothAppearance Appearance { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TPantsClothAppearance
    {
        public uint Clk { get; set; }
        public uint Cl { get; set; }
        public uint Mesh { get; set; }
        public byte HideSlotId { get; set; }
        public byte HidePartId { get; set; }
        public byte HideRaceId { get; set; }
    }
}

