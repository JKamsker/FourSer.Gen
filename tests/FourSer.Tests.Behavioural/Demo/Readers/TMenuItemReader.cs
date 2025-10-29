using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMenuItem.tcd")]
    [GenerateSerializer]
    public partial class TMenuItemCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TMenuItemEntry> Items { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TMenuItemEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Title { get; set; } = string.Empty;

        public uint ItemId { get; set; }
        public uint MenuId { get; set; }
        public byte GhostFlag { get; set; }
    }
}
