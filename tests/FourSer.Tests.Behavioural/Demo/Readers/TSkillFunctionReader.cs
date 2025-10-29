using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSkillFunction.tcd")]
    [GenerateSerializer]
    public partial class TSkillFunctionCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TSkillFunctionEntry> Functions { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TSkillFunctionEntry
    {
        public ushort SkillId { get; set; }
        public byte MethodId { get; set; }
        public byte TypeId { get; set; }
        public byte FunctionId { get; set; }
        public byte OpCode { get; set; }
        public byte CalculationType { get; set; }
        public ushort BaseValue { get; set; }
        public ushort Increment { get; set; }
    }
}

