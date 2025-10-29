using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMAP.tcd")]
    [GenerateSerializer]
    public partial class TMapCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TMapRecord> Maps { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TMapRecord
    {
        public uint ResourceId { get; set; }
        public uint MapId { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float ScaleZ { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float RotationX { get; set; }
        public float RotationY { get; set; }
        public float RotationZ { get; set; }
        public byte IsDungeon { get; set; }
        public byte AllowsNpcCall { get; set; }
    }
}

