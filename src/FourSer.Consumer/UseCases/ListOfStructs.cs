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
    public int Count { get; set; }
    
    [SerializeCollection(CountSizeReference = nameof(Count))]
    public List<ListOfStructsEntity> Structs { get; set; } = new();
    
}
