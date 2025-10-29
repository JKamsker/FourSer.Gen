using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TPet.tcd")]
    [GenerateSerializer]
    public partial class TPetCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TPetEntry> Pets { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TPetEntry
    {
        public ushort PetId { get; set; }
        public ushort MonsterTemplateId { get; set; }
        public byte RecallKindPrimary { get; set; }
        public byte RecallKindSecondary { get; set; }
        public ushort RecallValuePrimary { get; set; }
        public ushort RecallValueSecondary { get; set; }
        public byte ConditionType { get; set; }
        public uint ConditionValue { get; set; }
        public ushort IconId { get; set; }
    }
}

