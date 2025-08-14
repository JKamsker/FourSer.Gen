using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial struct ListOfStructsEntity
{
    public int A;
}

[GenerateSerializer]
public partial class ListOfStructs
{
    [SerializeCollection]
    public List<ListOfStructsEntity> Structs { get; set; } = new();
}