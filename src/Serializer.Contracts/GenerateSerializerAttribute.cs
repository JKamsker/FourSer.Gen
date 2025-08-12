using System;

namespace Serializer.Contracts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class GenerateSerializerAttribute : Attribute
{
}