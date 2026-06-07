using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("Tcashshop.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TCashShopResource
    {
        [Serializer(typeof(PlainAsciiStringSerializer))]
        public string Url { get; set; } = string.Empty;
    }
}
