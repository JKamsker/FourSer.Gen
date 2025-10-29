using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMonShop.tcd")]
    [GenerateSerializer]
    public partial class TMonShopCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TMonShopEntry> Shops { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TMonShopEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public uint ShopId { get; set; }
        public uint SpawnId { get; set; }
        public uint IconId { get; set; }
    }
}
