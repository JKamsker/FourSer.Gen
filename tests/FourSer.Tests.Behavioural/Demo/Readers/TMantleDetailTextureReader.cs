using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMantleDetailTexture.tcd")]
    [GenerateSerializer]
    public partial class TMantleDetailTextureCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TMantleDetailTextureEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TMantleDetailTextureEntry
    {
        public byte RaceId { get; set; }
        public byte SexId { get; set; }
        public uint MeshId { get; set; }
        public uint ConditionId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string MeshName { get; set; } = string.Empty;
    }
}
