using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TENV.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TEnvironmentCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TEnvironmentTrack> Tracks { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TEnvironmentTrack
    {
        public uint RegionId { get; set; }
        public uint EnvironmentId { get; set; }
    }
}
