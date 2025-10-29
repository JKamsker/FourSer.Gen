using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TENV.tcd")]
    [GenerateSerializer]
    public partial class TEnvironmentCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TEnvironmentTrack> Tracks { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TEnvironmentTrack
    {
        public uint RegionId { get; set; }
        public uint EnvironmentId { get; set; }
    }
}
