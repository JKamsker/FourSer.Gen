using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TFormula.tcd")]
    [GenerateSerializer]
    public partial class TFormulaCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TFormulaEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TFormulaEntry
    {
        public byte Id { get; set; }
        public uint InitialValue { get; set; }
        public float RateX { get; set; }
        public float RateY { get; set; }
    }
}
