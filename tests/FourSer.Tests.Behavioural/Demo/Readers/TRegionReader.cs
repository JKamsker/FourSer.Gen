using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TRegion.tcd")]
    [GenerateSerializer]
    public partial class TRegionCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TRegionEntry> Regions { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TRegionEntry
    {
        public uint RegionId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public byte CountryId { get; set; }
        public byte CanFly { get; set; }
        public ushort LocalId { get; set; }
        public float ThumbDX { get; set; }
        public float ThumbDY { get; set; }
        public float ThumbDZ { get; set; }
        public float ThumbCX { get; set; }
        public float ThumbCY { get; set; }
        public float ThumbCZ { get; set; }
        public float ThumbBX { get; set; }
        public float ThumbBY { get; set; }
        public float ThumbBZ { get; set; }
        public byte CanMail { get; set; }
        public uint EnvironmentSoundLoopId { get; set; }
        public uint BackgroundMusicId { get; set; }
        public uint EnvironmentSetId { get; set; }
        public byte InfoFlag { get; set; }
    }
}
