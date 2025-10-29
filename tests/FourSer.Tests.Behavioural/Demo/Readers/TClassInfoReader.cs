using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TClassInfo.tcd")]
    [GenerateSerializer]
    public partial class TClassInfoCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TClassInfoEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TClassInfoEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Text { get; set; } = string.Empty;

        public uint Id { get; set; }
    }
}
