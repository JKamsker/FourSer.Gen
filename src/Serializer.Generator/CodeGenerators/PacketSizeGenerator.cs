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
            sb.AppendLine($"        size += StringEx.MeasureSize(obj.{member.Name}); // Size for string {member.Name}");
        }
        else if (memberType.IsUnmanagedType)
        {
            sb.AppendLine($"        size += sizeof({memberType.ToDisplayString()}); // Size for unmanaged type {member.Name}");
        }
    }

    private static void GenerateCollectionSizeCalculation
    (
        StringBuilder sb,
        ISymbol member,
        ITypeSymbol memberType,
        AttributeData collectionAttribute,
        INamedTypeSymbol namedTypeSymbol
    )
    {
        var listTypeSymbol = (INamedTypeSymbol)memberType;
        var typeArgument = listTypeSymbol.TypeArguments[0];
        var countSizeReference = AttributeHelper.GetCountSizeReference(collectionAttribute);
        var countType = AttributeHelper.GetCountType(collectionAttribute);
        var countSize = AttributeHelper.GetCountSize(collectionAttribute);
        var polymorphicMode = (PolymorphicMode)AttributeHelper.GetPolymorphicMode(collectionAttribute);

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

        if (polymorphicMode == PolymorphicMode.None)
        {
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
        else
        {
            var typeIdType = AttributeHelper.GetCollectionTypeIdType(collectionAttribute);
            var typeIdSize = GetSizeOfType(typeIdType);
            var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);

            if (polymorphicMode == PolymorphicMode.SingleTypeId)
            {
                sb.AppendLine($"        size += {typeIdSize}; // Single TypeId for collection {member.Name}");
            }

            sb.AppendLine($"        foreach(var item in obj.{member.Name})");
            sb.AppendLine("        {");

            if (polymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.AppendLine($"            size += {typeIdSize}; // Individual TypeId for item in {member.Name}");
            }

            var isFirst = true;
            foreach (var option in polymorphicOptions)
            {
                var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                if (type != null)
                {
                    var typeReference = TypeAnalyzer.GetTypeReference(type, namedTypeSymbol);
                    var keyword = isFirst ? "if" : "else if";
                    sb.AppendLine($"            {keyword} (item is {typeReference} typedItem{type.Name})");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                size += {typeReference}.GetPacketSize(typedItem{type.Name});");
                    sb.AppendLine("            }");
                    isFirst = false;
                }
            }
            if (!isFirst)
            {
                sb.AppendLine("            else { throw new InvalidOperationException($\"Unknown polymorphic type in collection: {item.GetType().Name}\"); }");
            }
            sb.AppendLine("        }");
        }
    }

    private static void GeneratePolymorphicSizeCalculation(StringBuilder sb, ISymbol member, AttributeData polymorphicAttribute)
    {
        var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);
        var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);
        var typeIdType = AttributeHelper.GetTypeIdType(polymorphicAttribute);
        


        if (polymorphicOptions.Any())
        {
            sb.AppendLine($"        // Polymorphic size calculation for {member.Name} - infer type from actual object");
            
            // If no TypeId property is specified, we need to account for the TypeId we'll write
            if (string.IsNullOrEmpty(typeIdProperty))
            {
                var sizeOfType = GetSizeOfType(typeIdType);
                sb.AppendLine($"        size += {sizeOfType}; // TypeId for polymorphic {member.Name}");
            }
            
            // Use pattern matching instead of GetType().Name
            var isFirst = true;
            foreach (var option in polymorphicOptions)
            {
                var id = option.ConstructorArguments[0].Value;
                var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                if (type != null)
                {
                    var keyword = isFirst ? "if" : "else if";
                    sb.AppendLine($"        {keyword} (obj.{member.Name} is {type.Name} {member.Name.ToLower()}{type.Name})");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            size += {type.Name}.GetPacketSize({member.Name.ToLower()}{type.Name});");
                    sb.AppendLine("        }");
                    isFirst = false;
                }
            }
            
            if (!isFirst) // Only add else if we had any options
            {
                sb.AppendLine("        else");
                sb.AppendLine("        {");
                sb.AppendLine($"            throw new InvalidOperationException($\"Unknown polymorphic type: {{obj.{member.Name}.GetType().Name}}\");");
                sb.AppendLine("        }");
            }
        }
    }

    private static bool IsListType(ITypeSymbol memberType)
    {
        return memberType is INamedTypeSymbol listTypeSymbol && 
               listTypeSymbol.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>";
    }

    private static string GetSizeOfType(ITypeSymbol? typeIdType)
    {
        if (typeIdType == null) return "sizeof(int)";
        
        if (typeIdType.TypeKind == TypeKind.Enum)
        {
            var enumType = (INamedTypeSymbol)typeIdType;
            var underlyingType = enumType.EnumUnderlyingType?.Name ?? "int";
            return underlyingType switch
            {
                "Byte" => "sizeof(byte)",
                "UInt16" => "sizeof(ushort)",
                "Int64" => "sizeof(long)",
                _ => "sizeof(int)"
            };
        }
        
        return typeIdType.Name switch
        {
            "Byte" => "sizeof(byte)",
            "UInt16" => "sizeof(ushort)",
            "Int64" => "sizeof(long)",
            _ => "sizeof(int)"
        };
    }
}