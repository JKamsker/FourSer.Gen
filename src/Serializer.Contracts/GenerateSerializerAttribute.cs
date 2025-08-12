using System;

namespace Serializer.Contracts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Struct)]
public class GenerateSerializerAttribute : Attribute
{
    public Type? CountType { get; set; }
    public int CountSize { get; set; }
    public string? CountSizeReference { get; set; }
}