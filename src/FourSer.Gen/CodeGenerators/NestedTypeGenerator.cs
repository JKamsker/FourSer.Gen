using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using FourSer.Gen.CodeGenerators.Planning;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates serialization code for nested types
/// </summary>
internal static class NestedTypeGenerator
{
    internal static EquatableArray<MethodPlan> GenerateNestedTypes(
        IndentedStringBuilder sb,
        EquatableArray<TypeToGenerate> nestedTypes,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        var plans = new List<MethodPlan>();
        if (nestedTypes.IsEmpty)
        {
            return plans.ToEquatableArray();
        }

        foreach (var nestedType in nestedTypes)
        {
            sb.WriteLine();
            var typeKeyword = nestedType.IsValueType ? "struct" : "class";
            if (nestedType.IsRecord)
            {
                typeKeyword = $"record {typeKeyword}";
            }
            var disposableInterface = DisposalGenerator.ShouldGenerateDispose(nestedType) ? ", IDisposable" : string.Empty;
            sb.WriteLineFormat("public partial {0} {1} : ISerializable<{1}>{2}", typeKeyword, nestedType.Name, disposableInterface);
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

            plans.Add(PacketSizeGenerator.GenerateGetSize(sb, nestedType, options, capabilities));
            sb.WriteLine();
            plans.AddRange(DeserializationGenerator.GenerateDeserialize(sb, nestedType, options, capabilities));
            sb.WriteLine();
            plans.AddRange(SerializationGenerator.GenerateSerialize(sb, nestedType, options, capabilities));

            DisposalGenerator.GenerateDispose(sb, nestedType);

            // Handle even deeper nested types recursively
            if (!nestedType.NestedTypes.IsEmpty)
            {
                plans.AddRange(GenerateNestedTypes(sb, nestedType.NestedTypes, options, capabilities));
            }
        }

        return plans.ToEquatableArray();
    }
}
