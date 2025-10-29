using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TItemGradeVisual.tcd")]
    [GenerateSerializer]
    public partial class TItemGradeVisualCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItemGradeVisualRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TItemGradeVisualRecord
    {
        public byte Kind { get; set; }
        public ushort Grade { get; set; }
        public uint TextureId { get; set; }
        public byte OperationCode { get; set; }
    }
}
