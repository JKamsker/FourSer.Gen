using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSkillTree.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TSkillTreeCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TSkillTreeEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TSkillTreeEntry
    {
        public byte CountryId { get; set; }
        public byte ClassId { get; set; }
        public byte TabId { get; set; }
        public byte TreeIndex { get; set; }
        public ushort SkillId { get; set; }
    }
}

