using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TGodBall.tcd")]
    [GenerateSerializer]
    public partial class TGodBallCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TGodBallRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TGodBallRecord
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public uint Id { get; set; }
        public float Rotation { get; set; }
        public uint ObjectId { get; set; }
        public uint IconId { get; set; }
        public uint SfxId { get; set; }
    }
}
