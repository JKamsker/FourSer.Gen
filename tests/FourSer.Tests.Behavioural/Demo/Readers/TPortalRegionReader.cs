using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TPortalRegion.tcd")]
    [GenerateSerializer]
    public partial class TPortalRegionCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TPortalRegionEntry> Regions { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TPortalRegionEntry
    {
        public ushort RegionId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;
    }
}
