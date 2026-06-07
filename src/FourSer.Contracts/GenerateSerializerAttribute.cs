namespace FourSer.Contracts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class GenerateSerializerAttribute : Attribute
{
    public GenerateSerializerAttribute()
    {
    }

    public GenerateSerializerAttribute(SerializerGenerationMethods additionalMethods)
    {
        AdditionalMethods = additionalMethods;
    }

    public SerializerGenerationMethods AdditionalMethods { get; set; }
}
