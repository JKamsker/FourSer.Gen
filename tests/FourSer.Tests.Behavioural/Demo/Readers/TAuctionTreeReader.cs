using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TAuctionTree.tcd")]
    [GenerateSerializer]
    public partial class TAuctionTreeCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TAuctionTreeEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TAuctionTreeEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public uint EncodedId { get; set; }
    }
}
