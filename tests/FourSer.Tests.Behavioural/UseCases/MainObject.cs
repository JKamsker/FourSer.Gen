using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class MainObject
{
    [SerializeCollection(CountSize = 10)]
    public List<Item> Items { get; set; }
}
