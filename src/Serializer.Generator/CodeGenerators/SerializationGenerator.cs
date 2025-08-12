using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Serializer.Generator.CodeGenerators;

/// <summary>
/// Generates Serialize method implementations
/// </summary>
public static class SerializationGenerator
{
    public static void GenerateSerialize(StringBuilder sb, ClassToGenerate classToGenerate, INamedTypeSymbol namedTypeSymbol)
    {
        sb.AppendLine($"    public static int Serialize({classToGenerate.Name} obj, Span<byte> data)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var originalData = data;");

        // Generate helper method to infer TypeId from polymorphic properties
        GeneratePolymorphicTypeIdSynchronization(sb, classToGenerate);

        foreach (var member in classToGenerate.Members)
        {
            GenerateMemberSerialization(sb, member, namedTypeSymbol);
        }

        sb.AppendLine("        return originalData.Length - data.Length;");
        sb.AppendLine("    }");
    }

    private static void GeneratePolymorphicTypeIdSynchronization(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        var polymorphicMembers = classToGenerate.Members
            .Where(m => AttributeHelper.GetPolymorphicAttribute(m) != null)
            .ToList();

        if (polymorphicMembers.Any())
        {
            var hasTypeIdSynchronization = false;
            
            foreach (var polymorphicMember in polymorphicMembers)
            {
                var polymorphicAttribute = AttributeHelper.GetPolymorphicAttribute(polymorphicMember);
                var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(polymorphicMember);
                var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);

                // Only synchronize if there's a TypeId property specified
                if (!string.IsNullOrEmpty(typeIdProperty) && polymorphicOptions.Any())
                {
                    if (!hasTypeIdSynchronization)
                    {
                        sb.AppendLine("        // Ensure TypeId properties are synchronized with actual object types");
                        hasTypeIdSynchronization = true;
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
                            sb.AppendLine($"        {keyword} (obj.{polymorphicMember.Name} is {type.Name})");
                            sb.AppendLine("        {");
                            sb.AppendLine($"            obj.{typeIdProperty} = {id};");
                            sb.AppendLine("        }");
                            isFirst = false;
                        }
                    }
                    
                    if (!isFirst) // Only add else if we had any options
                    {
                        sb.AppendLine("        else");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            throw new InvalidOperationException($\"Unknown polymorphic type: {{obj.{polymorphicMember.Name}.GetType().Name}}\");");
                        sb.AppendLine("        }");
                    }
                }
            }
        }
    }

    private static void GenerateMemberSerialization(StringBuilder sb, ISymbol member, INamedTypeSymbol namedTypeSymbol)
    {
        var memberType = TypeAnalyzer.GetMemberType(member);
        var collectionAttribute = AttributeHelper.GetCollectionAttribute(member);
        var polymorphicAttribute = AttributeHelper.GetPolymorphicAttribute(member);

        if (collectionAttribute != null && IsListType(memberType))
        {
            GenerateCollectionSerialization(sb, member, memberType, collectionAttribute, namedTypeSymbol);
        }
        else if (polymorphicAttribute != null)
        {
            GeneratePolymorphicSerialization(sb, member, polymorphicAttribute);
        }
        else if (AttributeHelper.HasGenerateSerializerAttribute(memberType))
        {
            sb.AppendLine($"        var bytesWritten = {memberType.Name}.Serialize(obj.{member.Name}, data);");
            sb.AppendLine($"        data = data.Slice(bytesWritten);");
        }
        else if (memberType.SpecialType == SpecialType.System_String)
        {
            sb.AppendLine($"        data.WriteString(obj.{member.Name});");
        }
        else if (memberType.IsUnmanagedType)
        {
            sb.AppendLine($"        data.Write{memberType.Name}(obj.{member.Name});");
        }
    }

    private static void GenerateCollectionSerialization(StringBuilder sb, ISymbol member, ITypeSymbol memberType,
        AttributeData collectionAttribute, INamedTypeSymbol namedTypeSymbol)
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
                sb.AppendLine($"        data.Write{countType.Name}((ushort)obj.{member.Name}.Count);");
            }
            else if (countSize.HasValue && countSize != -1)
            {
                sb.AppendLine($"        data.WriteInt{countSize * 8}((ushort)obj.{member.Name}.Count);");
            }
            else
            {
                sb.AppendLine($"        data.WriteInt32(obj.{member.Name}.Count);");
            }
        }

        if (polymorphicMode == PolymorphicMode.None)
        {
            sb.AppendLine($"        for (int i = 0; i < obj.{member.Name}.Count; i++)");
            sb.AppendLine("        {");
            if (typeArgument.IsUnmanagedType)
            {
                sb.AppendLine($"            data.Write{typeArgument.Name}(obj.{member.Name}[i]);");
            }
            else
            {
                sb.AppendLine($"            var bytesWritten = {TypeAnalyzer.GetTypeReference(typeArgument, namedTypeSymbol)}.Serialize(obj.{member.Name}[i], data);");
                sb.AppendLine($"            data = data.Slice(bytesWritten);");
            }
            sb.AppendLine("        }");
        }
        else
        {
            var typeIdType = AttributeHelper.GetCollectionTypeIdType(collectionAttribute);
            var writeMethod = GetWriteMethod(typeIdType);
            var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);

            if (polymorphicMode == PolymorphicMode.SingleTypeId)
            {
                var typeIdProperty = AttributeHelper.GetCollectionTypeIdProperty(collectionAttribute);
                var castExpression = typeIdType?.TypeKind == TypeKind.Enum ? $"({GetUnderlyingTypeName(typeIdType)})obj.{typeIdProperty}" : $"obj.{typeIdProperty}";
                sb.AppendLine($"        data.{writeMethod}({castExpression});");
            }

            sb.AppendLine($"        foreach (var item in obj.{member.Name})");
            sb.AppendLine("        {");

            if (polymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                var isFirst = true;
                foreach (var option in polymorphicOptions)
                {
                    var id = option.ConstructorArguments[0].Value;
                    var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                    if (type != null)
                    {
                        var typeReference = TypeAnalyzer.GetTypeReference(type, namedTypeSymbol);
                        var keyword = isFirst ? "if" : "else if";
                        sb.AppendLine($"            {keyword} (item is {typeReference})");
                        sb.AppendLine("            {");
                        var idValue = FormatIdValue(id, typeIdType);
                        var castExpression = typeIdType?.TypeKind == TypeKind.Enum ? $"({GetUnderlyingTypeName(typeIdType)}){idValue}" : idValue;
                        sb.AppendLine($"                data.{writeMethod}({castExpression});");
                        sb.AppendLine("            }");
                        isFirst = false;
                    }
                }
                sb.AppendLine("            else { throw new InvalidOperationException($\"Unknown polymorphic type in collection: {item.GetType().Name}\"); }");
            }

            var isFirstSwitch = true;
            foreach (var option in polymorphicOptions)
            {
                var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                if (type != null)
                {
                    var typeReference = TypeAnalyzer.GetTypeReference(type, namedTypeSymbol);
                    var keyword = isFirstSwitch ? "if" : "else if";
                    sb.AppendLine($"            {keyword} (item is {typeReference} typedItem{type.Name})");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var bytesWritten{type.Name} = {typeReference}.Serialize(typedItem{type.Name}, data);");
                    sb.AppendLine($"                data = data.Slice(bytesWritten{type.Name});");
                    sb.AppendLine("            }");
                    isFirstSwitch = false;
                }
            }
            sb.AppendLine("        }");
        }
    }

    private static void GeneratePolymorphicSerialization(StringBuilder sb, ISymbol member, AttributeData polymorphicAttribute)
    {
        var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);
        var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);
        var typeIdType = AttributeHelper.GetTypeIdType(polymorphicAttribute);
        


        if (polymorphicOptions.Any())
        {
            sb.AppendLine($"        // Polymorphic serialization for {member.Name}");
            
            if (!string.IsNullOrEmpty(typeIdProperty))
            {
                // Use existing TypeId property
                sb.AppendLine($"        switch (obj.{typeIdProperty})");
            }
            else
            {
                // Infer TypeId from actual object type and write it directly using pattern matching
                var typeIdTypeName = GetTypeIdTypeName(typeIdType);
                var defaultValue = GetDefaultValue(typeIdType);
                var variableType = typeIdType?.ToDisplayString() ?? "int";
                
                sb.AppendLine($"        var {member.Name}TypeId = {defaultValue};");
                
                var isFirst = true;
                foreach (var option in polymorphicOptions)
                {
                    var id = option.ConstructorArguments[0].Value;
                    var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                    if (type != null)
                    {
                        var keyword = isFirst ? "if" : "else if";
                        sb.AppendLine($"        {keyword} (obj.{member.Name} is {type.Name})");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            {member.Name}TypeId = {FormatIdValue(id, typeIdType)};");
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
                
                var writeMethod = GetWriteMethod(typeIdType);
                var castExpression = typeIdType?.TypeKind == TypeKind.Enum 
                    ? $"({GetUnderlyingTypeName(typeIdType)}){member.Name}TypeId" 
                    : $"{member.Name}TypeId";
                sb.AppendLine($"        data.{writeMethod}({castExpression});");
                sb.AppendLine($"        switch ({member.Name}TypeId)");
            }
            
            sb.AppendLine("        {");

            foreach (var option in polymorphicOptions)
            {
                var id = option.ConstructorArguments[0].Value;
                var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                if (type != null)
                {
                    var caseValue = FormatCaseValue(id, typeIdType);
                    var safeId = GetSafeIdForVariableName(id);
                    sb.AppendLine($"            case {caseValue}:");
                    sb.AppendLine($"                var {member.Name}BytesWritten{safeId} = {type.Name}.Serialize(({type.Name})obj.{member.Name}, data);");
                    sb.AppendLine($"                data = data.Slice({member.Name}BytesWritten{safeId});");
                    sb.AppendLine("                break;");
                }
            }

            sb.AppendLine("            default:");
            if (!string.IsNullOrEmpty(typeIdProperty))
            {
                sb.AppendLine($"                throw new InvalidOperationException($\"Unknown type ID: {{obj.{typeIdProperty}}}\");");
            }
            else
            {
                sb.AppendLine($"                throw new InvalidOperationException($\"Unknown type ID: {{{member.Name}TypeId}}\");");
            }
            sb.AppendLine("        }");
        }
    }

    private static bool IsListType(ITypeSymbol memberType)
    {
        return memberType is INamedTypeSymbol listTypeSymbol && 
               listTypeSymbol.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>";
    }

    private static string GetTypeIdTypeName(ITypeSymbol? typeIdType)
    {
        if (typeIdType == null) return "int";
        
        if (typeIdType.TypeKind == TypeKind.Enum)
        {
            var enumType = (INamedTypeSymbol)typeIdType;
            return enumType.EnumUnderlyingType?.Name ?? "int";
        }
        
        return typeIdType.Name;
    }

    private static string GetDefaultValue(ITypeSymbol? typeIdType)
    {
        if (typeIdType == null) return "0";
        
        if (typeIdType.TypeKind == TypeKind.Enum)
        {
            return $"default({typeIdType.ToDisplayString()})";
        }
        
        return typeIdType.Name switch
        {
            "Byte" => "(byte)0",
            "UInt16" => "(ushort)0",
            "Int64" => "0L",
            _ => "0"
        };
    }

    private static string FormatIdValue(object? id, ITypeSymbol? typeIdType)
    {
        if (id == null) return "0";
        
        if (typeIdType?.TypeKind == TypeKind.Enum)
        {
            // For enums, we need to cast the underlying value, not the enum itself
            return $"({typeIdType.ToDisplayString()}){id}";
        }
        
        if (typeIdType != null)
        {
            return typeIdType.Name switch
            {
                "Byte" => $"(byte){id}",
                "UInt16" => $"(ushort){id}",
                "Int64" => $"{id}L",
                _ => id.ToString() ?? "0"
            };
        }
        
        return id.ToString() ?? "0";
    }

    private static string GetWriteMethod(ITypeSymbol? typeIdType)
    {
        if (typeIdType == null) return "WriteInt32";
        
        if (typeIdType.TypeKind == TypeKind.Enum)
        {
            var enumType = (INamedTypeSymbol)typeIdType;
            var underlyingType = enumType.EnumUnderlyingType?.Name ?? "int";
            return underlyingType switch
            {
                "Byte" => "WriteByte",
                "UInt16" => "WriteUInt16",
                "Int64" => "WriteInt64",
                _ => "WriteInt32"
            };
        }
        
        return typeIdType.Name switch
        {
            "Byte" => "WriteByte",
            "UInt16" => "WriteUInt16", 
            "Int64" => "WriteInt64",
            _ => "WriteInt32"
        };
    }

    private static string GetUnderlyingTypeName(ITypeSymbol? typeIdType)
    {
        if (typeIdType?.TypeKind == TypeKind.Enum)
        {
            var enumType = (INamedTypeSymbol)typeIdType;
            return enumType.EnumUnderlyingType?.Name ?? "int";
        }
        return typeIdType?.Name ?? "int";
    }

    private static string FormatCaseValue(object? id, ITypeSymbol? typeIdType)
    {
        if (id == null) return "0";
        
        if (typeIdType?.TypeKind == TypeKind.Enum)
        {
            return $"({typeIdType.ToDisplayString()}){id}";
        }
        
        return id.ToString() ?? "0";
    }

    private static string GetSafeIdForVariableName(object? id)
    {
        return id?.ToString()?.Replace("-", "Neg") ?? "0";
    }
}