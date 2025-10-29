using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSkillPoint.tcd")]
    [GenerateSerializer]
    public partial class TSkillPointCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TSkillPointEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TSkillPointEntry
    {
        public ushort SkillId { get; set; }
        public byte Level { get; set; }
        public byte SkillPoint { get; set; }
        public byte GroupPoint { get; set; }
        public byte PreviousLevel { get; set; }
    }
}

