using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("THelp.tcd")]
    [GenerateSerializer]
    public partial class THelpCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<THelpRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class THelpRecord
    {
        public uint CategoryId { get; set; }
        public ushort Page { get; set; }
        public ushort Image { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Title { get; set; } = string.Empty;

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Text { get; set; } = string.Empty;
    }
}
