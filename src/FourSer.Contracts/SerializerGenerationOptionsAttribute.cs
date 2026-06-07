namespace FourSer.Contracts;

[AttributeUsage(AttributeTargets.Assembly)]
public class SerializerGenerationOptionsAttribute : Attribute
{
    public SerializerGenerationOptionsAttribute(SerializerGenerationMethods additionalMethods)
    {
        AdditionalMethods = additionalMethods;
    }

    public SerializerGenerationMethods AdditionalMethods { get; set; }
}
