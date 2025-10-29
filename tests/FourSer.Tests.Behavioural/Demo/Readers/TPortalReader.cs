using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TPortal.tcd")]
    [GenerateSerializer]
    public partial class TPortalCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TPortalEntry> Portals { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TPortalEntry
    {
        public ushort PortalId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public ushort PortalRegionId { get; set; }
    }
}
