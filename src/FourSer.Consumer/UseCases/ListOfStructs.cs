using FourSer.Contracts;

namespace FourSer.Consumer.UseCases;

[GenerateSerializer]
public partial struct ListOfStructsEntity
{
    public int A;
}

[GenerateSerializer]
public partial class ListOfStructs
{
    [SerializeCollection(CountSizeReference = nameof(Count))]
    public List<ListOfStructsEntity> Structs { get; set; } = new();
    public int Count { get; set; }
}
