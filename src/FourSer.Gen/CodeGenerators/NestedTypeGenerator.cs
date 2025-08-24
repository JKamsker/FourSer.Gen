using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates serialization code for nested types
/// </summary>
public static class NestedTypeGenerator
{
    public static void GenerateNestedTypes(IndentedStringBuilder sb, EquatableArray<TypeToGenerate> nestedTypes)
    {
        if (nestedTypes.IsEmpty)
        {
            return;
        }

        foreach (var nestedType in nestedTypes)
        {
            sb.WriteLine();
            var typeKeyword = nestedType.IsValueType ? "struct" : "class";
            sb.WriteLineFormat("public partial {0} {1} : ISerializable<{1}>", typeKeyword, nestedType.Name);
            using var _ = sb.BeginBlock();
            // Delegate to the primary generators
            if (nestedType.Constructor is { ShouldGenerate: true } ctor)
            {
                if (!ctor.Parameters.IsEmpty)
                {
                    SerializerGenerator.GenerateConstructor(sb, nestedType, ctor);
                    sb.WriteLine();
                }

                if (!ctor.HasParameterlessConstructor)
                {
                    SerializerGenerator.GenerateParameterlessConstructor(sb, nestedType);
                    sb.WriteLine();
                }
            }

            PacketSizeGenerator.GenerateGetSize(sb, nestedType);
            sb.WriteLine();
            DeserializationGenerator.GenerateDeserialize(sb, nestedType);
            sb.WriteLine();
            SerializationGenerator.GenerateSerialize(sb, nestedType);

            // Handle even deeper nested types recursively
            if (!nestedType.NestedTypes.IsEmpty)
            {
                GenerateNestedTypes(sb, nestedType.NestedTypes);
            }
        }
    }
}