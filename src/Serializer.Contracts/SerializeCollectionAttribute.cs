using System;

namespace Serializer.Contracts;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializeCollectionAttribute : Attribute
{
    public Type? CountType { get; set; }
    public int CountSize { get; set; } = -1;
    public string? CountSizeReference { get; set; }
}

// Polymorphic attribute:
// [SerializePolymorphic(nameof(PropertyName))]
// [PolymorphicOption(1, typeof(Type1))]
// [PolymorphicOption(2, typeof(Type2))]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializePolymorphicAttribute : Attribute
{
    public string? PropertyName { get; set; }

    public SerializePolymorphicAttribute(string? propertyName = null)
    {
        PropertyName = propertyName;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class PolymorphicOptionAttribute : Attribute
{
    public int Id { get; }
    public Type Type { get; }

    public PolymorphicOptionAttribute(int id, Type type)
    {
        Id = id;
        Type = type;
    }
}