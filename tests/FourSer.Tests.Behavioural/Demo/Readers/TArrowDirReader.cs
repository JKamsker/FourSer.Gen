using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TArrowDIR.tcd")]
    [GenerateSerializer]
    public partial class TArrowDirectionCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TArrowDirectionEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TArrowDirectionEntry
    {
        public ushort SkillId { get; set; }
        public float Direction { get; set; }
    }
}
