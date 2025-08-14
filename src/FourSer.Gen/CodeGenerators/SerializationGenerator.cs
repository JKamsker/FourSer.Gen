using System.Text;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates Serialize method implementations
/// </summary>
public static class SerializationGenerator
{
    public static void GenerateSerialize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.AppendLine($"    public static int Serialize({typeToGenerate.Name} obj, System.Span<byte> data)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var originalData = data;");

        // Pre-pass to update TypeId properties for polymorphic members
        foreach (var member in typeToGenerate.Members)
        {
            if (member.PolymorphicInfo is not { } info || string.IsNullOrEmpty(info.TypeIdProperty))
            {
                continue;
            }

            if (member.IsCollection && member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                continue;
            }

            sb.AppendLine($"        switch (obj.{member.Name})");
            sb.AppendLine("        {");
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                var key = option.Key.ToString();
                if (info.EnumUnderlyingType is not null)
                {
                    key = $"({info.TypeIdType}){key}";
                }
                else if (info.TypeIdType.EndsWith("Enum"))
                {
                    key = $"{info.TypeIdType}.{key}";
                }
                sb.AppendLine($"            case {typeName}:");
                sb.AppendLine($"                obj.{info.TypeIdProperty} = {key};");
                sb.AppendLine("                break;");
            }
            sb.AppendLine("            case null:");
            sb.AppendLine("                break;");
            sb.AppendLine("        }");
        }

        foreach (var member in typeToGenerate.Members)
        {
            CodeGenerationHelper.GenerateMemberSerialization(sb, member);
        }

        sb.AppendLine("        return originalData.Length - data.Length;");
        sb.AppendLine("    }");
}
}
