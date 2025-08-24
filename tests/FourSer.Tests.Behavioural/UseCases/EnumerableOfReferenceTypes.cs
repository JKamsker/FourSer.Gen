using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class EnumerableOfReferenceTypes
{
    // public int Count { get; set; }
    
    [SerializeCollection(CountSize = 10)]
    public List<Entity> MyList { get; set; } 
}

[GenerateSerializer]
public partial class Entity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}


// [GenerateSerializer]
// public partial class PolymorphicEntity
// {
//     public int Id { get; set; }
//     public int TypeId { get; set; } 
//     // public int Count { get; set; }
//     
//     // [SerializePolymorphic(nameof(TypeId))]
//     [SerializeCollection(TypeIdProperty = "TypeId")]
//     [PolymorphicOption(1, typeof(EntityType1))]
//     [PolymorphicOption(2, typeof(EntityType2))]
//     public IEnumerable<BaseEntity> Entity { get; set; }
//     
//     [GenerateSerializer]
//     public partial class BaseEntity
//     {
//     }
//     
//     [GenerateSerializer]
//     public partial class EntityType1 : BaseEntity
//     {
//         public string Name { get; set; } = string.Empty;
//     }
//     
//     [GenerateSerializer]
//     public partial class EntityType2 : BaseEntity
//     {
//         public string Description { get; set; } = string.Empty;
//     }
// }
