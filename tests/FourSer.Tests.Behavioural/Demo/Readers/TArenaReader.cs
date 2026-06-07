using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TArena.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TArenaResource
    {
    }
}
