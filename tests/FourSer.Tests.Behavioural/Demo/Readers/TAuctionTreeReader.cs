using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TAuctionTree.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TAuctionTreeCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TAuctionTreeEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TAuctionTreeEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public uint EncodedId { get; set; }
    }
}
