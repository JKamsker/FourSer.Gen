using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TADEF.tcd")]
    [GenerateSerializer]
    public partial class TActionDefinitionCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TActionDefinition> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TActionDefinition
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Command { get; set; } = string.Empty;

        public byte Level { get; set; }
        public byte SubAction { get; set; }
        public byte LoopAction { get; set; }
        public byte ContinueAction { get; set; }
        public byte CancelAction { get; set; }
        public byte NavigationAction { get; set; }
        public byte SkipMain { get; set; }
        public byte HideOnCapeMode { get; set; }
    }
}
