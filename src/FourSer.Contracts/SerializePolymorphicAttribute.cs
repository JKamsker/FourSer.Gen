namespace FourSer.Contracts;

// Polymorphic attribute:
// [SerializePolymorphic(nameof(PropertyName))]
// [SerializePolymorphic(TypeIdType = typeof(byte))]
// [SerializePolymorphic(nameof(PropertyName), TypeIdType = typeof(MyEnum))]
// [PolymorphicOption(1, typeof(Type1))]
// [PolymorphicOption(2, typeof(Type2))]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializePolymorphicAttribute : Attribute
{
    public string? PropertyName { get; set; }
    public Type? TypeIdType { get; set; }

    public SerializePolymorphicAttribute(string? propertyName = null)
    {
        PropertyName = propertyName;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class PolymorphicOptionAttribute : Attribute
{
    public object Id { get; }
    public Type Type { get; }
    public bool IsDefault { get; set; }

    public PolymorphicOptionAttribute(int id, Type type)
        : this(id, type, false)
    {
    }

    public PolymorphicOptionAttribute(int id, Type type, bool isDefault)
    {
        Id = id;
        Type = type;
        IsDefault = isDefault;
    }

    public PolymorphicOptionAttribute(byte id, Type type)
        : this(id, type, false)
    {
    }

    public PolymorphicOptionAttribute(byte id, Type type, bool isDefault)
    {
        Id = id;
        Type = type;
        IsDefault = isDefault;
    }

    public PolymorphicOptionAttribute(ushort id, Type type)
        : this(id, type, false)
    {
    }

    public PolymorphicOptionAttribute(ushort id, Type type, bool isDefault)
    {
        Id = id;
        Type = type;
        IsDefault = isDefault;
    }

    public PolymorphicOptionAttribute(long id, Type type)
        : this(id, type, false)
    {
    }

    public PolymorphicOptionAttribute(long id, Type type, bool isDefault)
    {
        Id = id;
        Type = type;
        IsDefault = isDefault;
    }

    // For enum values
    public PolymorphicOptionAttribute(object id, Type type)
        : this(id, type, false)
    {
    }

    public PolymorphicOptionAttribute(object id, Type type, bool isDefault)
    {
        Id = id;
        Type = type;
        IsDefault = isDefault;
    }
}
