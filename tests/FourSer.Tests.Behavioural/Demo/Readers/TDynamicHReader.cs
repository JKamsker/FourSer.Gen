using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TDynamicH.tcd")]
    [GenerateSerializer]
    public partial class TDynamicHelpCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TDynamicHelpEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TDynamicHelpEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Content { get; set; } = string.Empty;

        public uint Id { get; set; }
    }
}
