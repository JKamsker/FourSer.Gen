using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class MixedFieldsAndPropsPacket
{
    // Properties (should be serialized)
    public int PropertyInt { get; set; }
    public string PropertyString { get; set; } = string.Empty;
    
    // Public fields (should now be serialized)
    public int FieldInt;
    public string FieldString = string.Empty;
    public float FieldFloat;
        
#pragma warning disable CS0169 // Field is never used
    // Private field (should NOT be serialized)
    // ReSharper disable once InconsistentNaming
    private int privateField;
#pragma warning restore CS0169 // Field is never used
    
    // Read-only field (should NOT be serialized)
    public readonly int ReadOnlyField = 42;
    
    // Property without setter (should NOT be serialized)
    public int ReadOnlyProperty => 123;
}

public class MixedFieldsAndPropsTest
{
    [Fact]
    public void MixedFieldsAndPropertiesTest()
    {
        var original = new MixedFieldsAndPropsPacket
        {
            PropertyInt = 123,
            PropertyString = "TestProperty",
            FieldInt = 456,
            FieldString = "TestField",
            FieldFloat = 3.14f
        };

        var size = MixedFieldsAndPropsPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        MixedFieldsAndPropsPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = MixedFieldsAndPropsPacket.Deserialize(readOnlySpan);

        // Verify properties are serialized/deserialized
        Assert.Equal(original.PropertyInt, deserialized.PropertyInt);
        Assert.Equal(original.PropertyString, deserialized.PropertyString);
        
        // Verify public fields are serialized/deserialized
        Assert.Equal(original.FieldInt, deserialized.FieldInt);
        Assert.Equal(original.FieldString, deserialized.FieldString);
        Assert.Equal(original.FieldFloat, deserialized.FieldFloat);
    }
}