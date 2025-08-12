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
            sb.AppendLine("        // Ensure TypeId properties are synchronized with actual object types");
            foreach (var polymorphicMember in polymorphicMembers)
            {
                var polymorphicAttribute = AttributeHelper.GetPolymorphicAttribute(polymorphicMember);
                var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(polymorphicMember);
                var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);

                if (!string.IsNullOrEmpty(typeIdProperty) && polymorphicOptions.Any())
                {
                    sb.AppendLine($"        var actualType{polymorphicMember.Name} = obj.{polymorphicMember.Name}.GetType().Name;");
                    sb.AppendLine($"        switch (actualType{polymorphicMember.Name})");
                    sb.AppendLine("        {");

                    foreach (var option in polymorphicOptions)
                    {
                        var id = option.ConstructorArguments[0].Value;
                        var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                        if (type != null)
                        {
                            sb.AppendLine($"            case \"{type.Name}\":");
                            sb.AppendLine($"                obj.{typeIdProperty} = {id};");
                            sb.AppendLine("                break;");
                        }
                    }

                    sb.AppendLine("            default:");
                    sb.AppendLine($"                throw new InvalidOperationException($\"Unknown polymorphic type: {{actualType{polymorphicMember.Name}}}\");");
                    sb.AppendLine("        }");
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

    private static void GeneratePolymorphicSerialization(StringBuilder sb, ISymbol member, AttributeData polymorphicAttribute)
    {
        var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);
        var typeIdProperty = AttributeHelper.GetTypeIdProperty(polymorphicAttribute);

        if (!string.IsNullOrEmpty(typeIdProperty) && polymorphicOptions.Any())
        {
            sb.AppendLine($"        // Polymorphic serialization for {member.Name}");
            sb.AppendLine($"        switch (obj.{typeIdProperty})");
            sb.AppendLine("        {");

            foreach (var option in polymorphicOptions)
            {
                var id = option.ConstructorArguments[0].Value;
                var type = option.ConstructorArguments[1].Value as ITypeSymbol;
                if (type != null)
                {
                    sb.AppendLine($"            case {id}:");
                    sb.AppendLine($"                var {member.Name}BytesWritten{id} = {type.Name}.Serialize(({type.Name})obj.{member.Name}, data);");
                    sb.AppendLine($"                data = data.Slice({member.Name}BytesWritten{id});");
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