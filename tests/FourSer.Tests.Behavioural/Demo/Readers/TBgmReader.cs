using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TBGM.tcd")]
    [GenerateSerializer]
    public partial class TBgmCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TBgmTrack> Tracks { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TBgmTrack
    {
        public uint RegionId { get; set; }
        public uint BgmId { get; set; }
    }
}
