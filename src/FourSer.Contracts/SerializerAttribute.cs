namespace FourSer.Contracts;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializerAttribute : Attribute
{
    public Type SerializerType { get; }

    public SerializerAttribute(Type serializerType)
    {
        SerializerType = serializerType;
    }
}
