using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class PolymorphicWithByteTypeId
{
    public int Id { get; set; }
    
    [SerializePolymorphic(TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(ByteEntityType1))]
    [PolymorphicOption((byte)2, typeof(ByteEntityType2))]
    public BaseByteEntity? Entity { get; set; }
    
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class BaseByteEntity
    {
    }
    
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class ByteEntityType1 : BaseByteEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
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