namespace FourSer.Contracts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, AllowMultiple = true)]
public class DefaultSerializerAttribute : Attribute
{
    public Type TargetType { get; }
    public Type SerializerType { get; }

    public DefaultSerializerAttribute(Type targetType, Type serializerType)
    {
        TargetType = targetType;
        SerializerType = serializerType;
    }
}
