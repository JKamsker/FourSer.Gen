using System;

namespace FourSer.Contracts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class GenerateSerializerAttribute : Attribute
{
}