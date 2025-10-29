using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("THelpLink.tcd")]
    [GenerateSerializer]
    public partial class THelpLinkCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<THelpLinkRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class THelpLinkRecord
    {
        public uint QuestId { get; set; }
        public byte Trigger { get; set; }
        public uint HelpId { get; set; }
    }
}
