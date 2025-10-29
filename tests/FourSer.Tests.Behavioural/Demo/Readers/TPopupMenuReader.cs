using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TPopupMenu.tcd")]
    [GenerateSerializer]
    public partial class TPopupMenuCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TPopupMenuLink> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TPopupMenuLink
    {
        public uint PopupId { get; set; }
        public uint MenuItemId { get; set; }
    }
}

