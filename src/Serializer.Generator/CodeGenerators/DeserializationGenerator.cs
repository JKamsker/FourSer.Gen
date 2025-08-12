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

    private static void GeneratePolymorphicDeserialization(StringBuilder sb, ISymbol member, AttributeData polymorphicAttribute)
    {
        var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);
        var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);

        if (!string.IsNullOrEmpty(typeIdProperty) && polymorphicOptions.Any())
        {
            sb.AppendLine($"        // Polymorphic deserialization for {member.Name}");
            sb.AppendLine($"        switch (obj.{typeIdProperty})");
            sb.AppendLine("        {");

            foreach (var option in polymorphicOptions)
            {
                var id = option.ConstructorArguments[0].Value;
                var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                if (type != null)
                {
                    sb.AppendLine($"            case {id}:");
                    sb.AppendLine($"                obj.{member.Name} = {type.Name}.Deserialize(data, out var {member.Name}BytesRead{id});");
                    sb.AppendLine($"                data = data.Slice({member.Name}BytesRead{id});");
                    sb.AppendLine("                break;");
                }
            }

            sb.AppendLine("            default:");
            sb.AppendLine($"                throw new InvalidOperationException($\"Unknown type ID: {{obj.{typeIdProperty}}}\");");
            sb.AppendLine("        }");
        }
    }

    private static bool IsListType(ITypeSymbol memberType)
    {
        return memberType is INamedTypeSymbol listTypeSymbol && 
               listTypeSymbol.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>";
    }
}