using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TItemGrade.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TItemGradeCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItemGradeRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TItemGradeRecord
    {
        public byte Level { get; set; }
        public byte Grade { get; set; }
    }
}
