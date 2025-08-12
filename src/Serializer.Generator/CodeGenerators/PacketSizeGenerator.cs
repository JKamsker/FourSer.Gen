using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Serializer.Generator.CodeGenerators;

/// <summary>
/// Generates GetPacketSize method implementations
/// </summary>
public static class PacketSizeGenerator
{
    public static void GenerateGetPacketSize(StringBuilder sb, ClassToGenerate classToGenerate, INamedTypeSymbol namedTypeSymbol)
    {
        sb.AppendLine($"    public static int GetPacketSize({classToGenerate.Name} obj)");
        sb.AppendLine("    {");
        sb.AppendLine("        var size = 0;");

        foreach (var member in classToGenerate.Members)
        {
            GenerateMemberSizeCalculation(sb, member, namedTypeSymbol);
        }

        sb.AppendLine("        return size;");
        sb.AppendLine("    }");
    }

    private static void GenerateMemberSizeCalculation(StringBuilder sb, ISymbol member, INamedTypeSymbol namedTypeSymbol)
    {
        var memberType = TypeAnalyzer.GetMemberType(member);
        var collectionAttribute = AttributeHelper.GetCollectionAttribute(member);
        var polymorphicAttribute = AttributeHelper.GetPolymorphicAttribute(member);

        if (collectionAttribute != null && IsListType(memberType))
        {
            GenerateCollectionSizeCalculation(sb, member, memberType, collectionAttribute, namedTypeSymbol);
        }
        else if (polymorphicAttribute != null)
        {
            GeneratePolymorphicSizeCalculation(sb, member, polymorphicAttribute);
        }
        else if (AttributeHelper.HasGenerateSerializerAttribute(memberType))
        {
            sb.AppendLine($"        size += {memberType.Name}.GetPacketSize(obj.{member.Name});");
        }
        else if (memberType.SpecialType == SpecialType.System_String)
        {
            sb.AppendLine($"        size += sizeof(int); // Size for string length");
            sb.AppendLine($"        size += System.Text.Encoding.UTF8.GetByteCount(obj.{member.Name});");
        }
        else if (memberType.IsUnmanagedType)
        {
            sb.AppendLine($"        size += sizeof({memberType.ToDisplayString()}); // Size for unmanaged type {member.Name}");
        }
    }

    private static void GenerateCollectionSizeCalculation(StringBuilder sb, ISymbol member, ITypeSymbol memberType, 
        AttributeData collectionAttribute, INamedTypeSymbol namedTypeSymbol)
    {
        var listTypeSymbol = (INamedTypeSymbol)memberType;
        var typeArgument = listTypeSymbol.TypeArguments[0];
        var countSizeReference = AttributeHelper.GetCountSizeReference(collectionAttribute);
        var countType = AttributeHelper.GetCountType(collectionAttribute);
        var countSize = AttributeHelper.GetCountSize(collectionAttribute);

        if (string.IsNullOrEmpty(countSizeReference))
        {
            if (countType != null)
            {
                sb.AppendLine($"        size += sizeof({countType.ToDisplayString()}); // Count size for {member.Name}");
            }
            else if (countSize.HasValue && countSize != -1)
            {
                sb.AppendLine($"        size += {countSize.Value}; // Count size for {member.Name} (in bits)");
            }
            else
            {
                sb.AppendLine($"        size += sizeof(int); // Default count size for {member.Name}");
            }
        }

        if (typeArgument.IsUnmanagedType)
        {
            sb.AppendLine($"        size += obj.{member.Name}.Count * sizeof({typeArgument.ToDisplayString()});");
        }
        else
        {
            sb.AppendLine($"        foreach(var item in obj.{member.Name})");
            sb.AppendLine("        {");
            sb.AppendLine($"            size += {TypeAnalyzer.GetTypeReference(typeArgument, namedTypeSymbol)}.GetPacketSize(item);");
            sb.AppendLine("        }");
        }
    }

    private static void GeneratePolymorphicSizeCalculation(StringBuilder sb, ISymbol member, AttributeData polymorphicAttribute)
    {
        var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);
        var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);

        if (!string.IsNullOrEmpty(typeIdProperty) && polymorphicOptions.Any())
        {
            sb.AppendLine($"        // Polymorphic size calculation for {member.Name} - infer type from actual object");
            sb.AppendLine($"        switch (obj.{member.Name}.GetType().Name)");
            sb.AppendLine("        {");

            foreach (var option in polymorphicOptions)
            {
                var id = option.ConstructorArguments[0].Value;
                var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                if (type != null)
                {
                    sb.AppendLine($"            case \"{type.Name}\":");
                    sb.AppendLine($"                size += {type.Name}.GetPacketSize(({type.Name})obj.{member.Name});");
                    sb.AppendLine("                break;");
                }
            }

            sb.AppendLine("            default:");
            sb.AppendLine($"                throw new InvalidOperationException($\"Unknown polymorphic type: {{obj.{member.Name}.GetType().Name}}\");");
            sb.AppendLine("        }");
        }
    }

    private static bool IsListType(ITypeSymbol memberType)
    {
        return memberType is INamedTypeSymbol listTypeSymbol && 
               listTypeSymbol.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>";
    }
}