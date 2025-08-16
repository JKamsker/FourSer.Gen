using FourSer.Contracts;

namespace FourSer.Consumer.UseCases;

[GenerateSerializer]
public partial class PolymorphicWithByteTypeId
{
    public int Id { get; set; }
    
    [SerializePolymorphic(TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(ByteEntityType1))]
    [PolymorphicOption((byte)2, typeof(ByteEntityType2))]
    public BaseByteEntity? Entity { get; set; }
    
    [GenerateSerializer]
    public partial class BaseByteEntity
    {
    }
    
    [GenerateSerializer]
    public partial class ByteEntityType1 : BaseByteEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer]
    public partial class ByteEntityType2 : BaseByteEntity
    {
        public string Description { get; set; } = string.Empty;
    }
}

public class PolymorphicWithByteTypeIdTests
{
    public static void RunTest()
    {
        // Test byte TypeId
        var byteEntity = new PolymorphicWithByteTypeId
        {
            Id = 100,
            Entity = new PolymorphicWithByteTypeId.ByteEntityType1 { Name = "Byte TypeId Test" }
        };
        
        PolymorphicTypeIdTest.TestSerialization(byteEntity, "Byte TypeId", 
            () => PolymorphicWithByteTypeId.GetPacketSize(byteEntity),
            (buffer) => PolymorphicWithByteTypeId.Serialize(byteEntity, buffer),
            (buffer) => PolymorphicWithByteTypeId.Deserialize(buffer));
    }
}