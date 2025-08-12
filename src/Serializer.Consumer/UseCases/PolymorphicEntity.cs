using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class PolymorphicEntity
{
    public int Id { get; set; }
    public int TypeId { get; set; } // Used to identify the type during deserialization
    
    [SerializePolymorphic(nameof(TypeId))]
    [PolymorphicOption(1, typeof(EntityType1))]
    public BaseEntity Entity { get; set; }
    
    [GenerateSerializer]
    public partial class BaseEntity
    {
    }
    
    [GenerateSerializer]
    public partial class EntityType1 : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer]
    public partial class EntityType2 : BaseEntity
    {
        public string Description { get; set; } = string.Empty;
    }
}