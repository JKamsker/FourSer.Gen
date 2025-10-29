using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TLIGHT.tcd")]
    [GenerateSerializer]
    public partial class TLightCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TLightEntry> Lights { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TLightEntry
    {
        public uint LightId { get; set; }
        public float DirectionX { get; set; }
        public float DirectionY { get; set; }
        public float DirectionZ { get; set; }
        public float AmbientR { get; set; }
        public float AmbientG { get; set; }
        public float AmbientB { get; set; }
        public float DiffuseR { get; set; }
        public float DiffuseG { get; set; }
        public float DiffuseB { get; set; }
    }
}

