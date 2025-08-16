using System.Text;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates serialization code for nested types
/// </summary>
public static class NestedTypeGenerator
{
    public static void GenerateNestedTypes(StringBuilder sb, EquatableArray<TypeToGenerate> nestedTypes)
    {
        if (nestedTypes.IsEmpty)
        {
            return;
        }

        foreach (var nestedType in nestedTypes)
        {
            sb.AppendLine();
            var typeKeyword = nestedType.IsValueType ? "struct" : "class";
            sb.AppendLine($"    public partial {typeKeyword} {nestedType.Name} : ISerializable<{nestedType.Name}>");
            sb.AppendLine("    {");

            // Delegate to the primary generators
<<<<<<< HEAD
            if (nestedType.Constructor is { ShouldGenerate: true } ctor)
            {
                if (!ctor.Parameters.IsEmpty)
                {
                    SerializerGenerator.GenerateConstructor(sb, nestedType, ctor);
                    sb.AppendLine();
                }

                if (!ctor.HasPublicParameterlessConstructor)
                {
                    SerializerGenerator.GenerateParameterlessConstructor(sb, nestedType);
                    sb.AppendLine();
                }
            }

=======
>>>>>>> main
            PacketSizeGenerator.GenerateGetPacketSize(sb, nestedType);
            sb.AppendLine();
            DeserializationGenerator.GenerateDeserialize(sb, nestedType);
            sb.AppendLine();
            SerializationGenerator.GenerateSerialize(sb, nestedType);

            // Handle even deeper nested types recursively
            if (!nestedType.NestedTypes.IsEmpty)
            {
                GenerateNestedTypes(sb, nestedType.NestedTypes);
            }

            sb.AppendLine("    }");
        }
    }
}