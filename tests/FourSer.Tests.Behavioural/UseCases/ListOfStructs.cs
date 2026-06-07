using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial struct ListOfStructsEntity
{
    public int A;
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class ListOfStructs
{
    public int Count { get; set; }
    
    [SerializeCollection(CountSizeReference = nameof(Count))]
    public List<ListOfStructsEntity> Structs { get; set; } = new();
    
}
