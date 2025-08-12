using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Serializer.Generator.CodeGenerators;

/// <summary>
/// Generates Deserialize method implementations
/// </summary>
public static class DeserializationGenerator
{
    public static void GenerateDeserialize(StringBuilder sb, ClassToGenerate classToGenerate, INamedTypeSymbol namedTypeSymbol)
    {
        sb.AppendLine($"    public static {classToGenerate.Name} Deserialize(ReadOnlySpan<byte> data, out int bytesRead)");
        sb.AppendLine("    {");
        sb.AppendLine($"        bytesRead = 0;");
        sb.AppendLine($"        var originalData = data;");
        sb.AppendLine($"        var obj = new {classToGenerate.Name}();");

        foreach (var member in classToGenerate.Members)
        {
            GenerateMemberDeserialization(sb, member, namedTypeSymbol);
        }

        sb.AppendLine("        bytesRead = originalData.Length - data.Length;");
        sb.AppendLine("        return obj;");
        sb.AppendLine("    }");
    }

    private static void GenerateMemberDeserialization(StringBuilder sb, ISymbol member, INamedTypeSymbol namedTypeSymbol)
    {
        var memberType = TypeAnalyzer.GetMemberType(member);
        var collectionAttribute = AttributeHelper.GetCollectionAttribute(member);
        var polymorphicAttribute = AttributeHelper.GetPolymorphicAttribute(member);

        if (collectionAttribute != null && IsListType(memberType))
        {
            GenerateCollectionDeserialization(sb, member, memberType, collectionAttribute, namedTypeSymbol);
        }
        else if (polymorphicAttribute != null)
        {
            GeneratePolymorphicDeserialization(sb, member, polymorphicAttribute);
        }
        else if (AttributeHelper.HasGenerateSerializerAttribute(memberType))
        {
            sb.AppendLine($"        obj.{member.Name} = {memberType.Name}.Deserialize(data, out var nestedBytesRead);");
            sb.AppendLine($"        data = data.Slice(nestedBytesRead);");
        }
        else if (memberType.SpecialType == SpecialType.System_String)
        {
            sb.AppendLine($"        obj.{member.Name} = data.ReadString();");
        }
        else if (memberType.IsUnmanagedType)
        {
            sb.AppendLine($"        obj.{member.Name} = data.Read{memberType.Name}();");
        }
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, ISymbol member, ITypeSymbol memberType,
        AttributeData collectionAttribute, INamedTypeSymbol namedTypeSymbol)
    {
        var listTypeSymbol = (INamedTypeSymbol)memberType;
        var typeArgument = listTypeSymbol.TypeArguments[0];
        var countSizeReference = AttributeHelper.GetCountSizeReference(collectionAttribute);
        var countType = AttributeHelper.GetCountType(collectionAttribute);
        var countSize = AttributeHelper.GetCountSize(collectionAttribute);
        var polymorphicMode = (PolymorphicMode)AttributeHelper.GetPolymorphicMode(collectionAttribute);

        sb.AppendLine($"        var {member.Name}Count = 0;");
        if (!string.IsNullOrEmpty(countSizeReference))
        {
            sb.AppendLine($"        {member.Name}Count = (int)obj.{countSizeReference};");
        }
        else if (countType != null)
        {
            sb.AppendLine($"        {member.Name}Count = data.Read{countType.Name}();");
        }
        else if (countSize.HasValue && countSize != -1)
        {
            sb.AppendLine($"        {member.Name}Count = data.ReadInt{countSize * 8}();");
        }
        else
        {
            sb.AppendLine($"        {member.Name}Count = data.ReadInt32();");
        }

        sb.AppendLine($"        obj.{member.Name} = new System.Collections.Generic.List<{typeArgument.ToDisplayString()}>({member.Name}Count);");

        if (polymorphicMode == PolymorphicMode.None)
        {
            sb.AppendLine($"        for (int i = 0; i < {member.Name}Count; i++)");
            sb.AppendLine("        {");
            if (typeArgument.IsUnmanagedType)
            {
                sb.AppendLine($"            obj.{member.Name}.Add(data.Read{typeArgument.Name}());");
            }
            else
            {
                sb.AppendLine($"            obj.{member.Name}.Add({TypeAnalyzer.GetTypeReference(typeArgument, namedTypeSymbol)}.Deserialize(data, out var itemBytesRead));");
                sb.AppendLine($"            data = data.Slice(itemBytesRead);");
            }
            sb.AppendLine("        }");
        }
        else
        {
            var typeIdType = AttributeHelper.GetCollectionTypeIdType(collectionAttribute);
            var readMethod = GetReadMethod(typeIdType);
            var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);
            var typeIdVarType = typeIdType?.ToDisplayString() ?? "int";

            if (polymorphicMode == PolymorphicMode.SingleTypeId)
            {
                var castExpression = typeIdType?.TypeKind == TypeKind.Enum ? $"({typeIdVarType})data.{readMethod}()" : $"data.{readMethod}()";
                sb.AppendLine($"        var {member.Name}TypeId = {castExpression};");
                sb.AppendLine($"        for (int i = 0; i < {member.Name}Count; i++)");
                sb.AppendLine("        {");
                sb.AppendLine($"            switch ({member.Name}TypeId)");
                sb.AppendLine("            {");
                foreach (var option in polymorphicOptions)
                {
                    var id = option.ConstructorArguments[0].Value;
                    var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                    if (type != null)
                    {
                        var typeReference = TypeAnalyzer.GetTypeReference(type, namedTypeSymbol);
                        var caseValue = FormatCaseValue(id, typeIdType);
                        var safeId = GetSafeIdForVariableName(id);
                        sb.AppendLine($"                case {caseValue}:");
                        sb.AppendLine($"                    obj.{member.Name}.Add({typeReference}.Deserialize(data, out var itemBytesRead{safeId}));");
                        sb.AppendLine($"                    data = data.Slice(itemBytesRead{safeId});");
                        sb.AppendLine("                    break;");
                    }
                }
                sb.AppendLine($"                default: throw new InvalidOperationException($\"Unknown type ID: {{{member.Name}TypeId}}\");");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }
            else // IndividualTypeIds
            {
                sb.AppendLine($"        for (int i = 0; i < {member.Name}Count; i++)");
                sb.AppendLine("        {");
                var castExpression = typeIdType?.TypeKind == TypeKind.Enum ? $"({typeIdVarType})data.{readMethod}()" : $"data.{readMethod}()";
                sb.AppendLine($"            var itemTypeId = {castExpression};");
                sb.AppendLine("            switch (itemTypeId)");
                sb.AppendLine("            {");
                foreach (var option in polymorphicOptions)
                {
                    var id = option.ConstructorArguments[0].Value;
                    var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                    if (type != null)
                    {
                        var typeReference = TypeAnalyzer.GetTypeReference(type, namedTypeSymbol);
                        var caseValue = FormatCaseValue(id, typeIdType);
                        var safeId = GetSafeIdForVariableName(id);
                        sb.AppendLine($"                case {caseValue}:");
                        sb.AppendLine($"                    obj.{member.Name}.Add({typeReference}.Deserialize(data, out var itemBytesRead{safeId}));");
                        sb.AppendLine($"                    data = data.Slice(itemBytesRead{safeId});");
                        sb.AppendLine("                    break;");
                    }
                }
                sb.AppendLine("                default: throw new InvalidOperationException($\"Unknown type ID: {itemTypeId}\");");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }
        }
    }

    private static void GeneratePolymorphicDeserialization(StringBuilder sb, ISymbol member, AttributeData polymorphicAttribute)
    {
        var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);
        var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);
        var typeIdType = AttributeHelper.GetTypeIdType(polymorphicAttribute);
        


        if (polymorphicOptions.Any())
        {
            sb.AppendLine($"        // Polymorphic deserialization for {member.Name}");
            
            if (!string.IsNullOrEmpty(typeIdProperty))
            {
                // Use existing TypeId property
                sb.AppendLine($"        switch (obj.{typeIdProperty})");
            }
            else
            {
                // Read TypeId directly from stream without storing in model
                var readMethod = GetReadMethod(typeIdType);
                var variableType = typeIdType?.ToDisplayString() ?? "int";
                var castExpression = typeIdType?.TypeKind == TypeKind.Enum 
                    ? $"({variableType})data.{readMethod}()" 
                    : $"data.{readMethod}()";
                sb.AppendLine($"        var {member.Name}TypeId = {castExpression};");
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
                    sb.AppendLine($"                obj.{member.Name} = {type.Name}.Deserialize(data, out var {member.Name}BytesRead{safeId});");
                    sb.AppendLine($"                data = data.Slice({member.Name}BytesRead{safeId});");
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

    private static string GetReadMethod(ITypeSymbol? typeIdType)
    {
        if (typeIdType == null) return "ReadInt32";
        
        if (typeIdType.TypeKind == TypeKind.Enum)
        {
            var enumType = (INamedTypeSymbol)typeIdType;
            var underlyingType = enumType.EnumUnderlyingType?.Name ?? "int";
            return underlyingType switch
            {
                "Byte" => "ReadByte",
                "UInt16" => "ReadUInt16",
                "Int64" => "ReadInt64",
                _ => "ReadInt32"
            };
        }
        
        return typeIdType.Name switch
        {
            "Byte" => "ReadByte",
            "UInt16" => "ReadUInt16",
            "Int64" => "ReadInt64",
            _ => "ReadInt32"
        };
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