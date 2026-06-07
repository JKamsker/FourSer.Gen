using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class Item
{
    public int Id { get; set; }
}
