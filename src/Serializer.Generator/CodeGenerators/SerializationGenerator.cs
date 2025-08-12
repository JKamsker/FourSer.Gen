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
                sb.AppendLine($"        var {member.Name}TypeId = 0;");
                
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
                        sb.AppendLine($"            {member.Name}TypeId = {id};");
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
                
                sb.AppendLine($"        data.WriteInt32({member.Name}TypeId);");
                sb.AppendLine($"        switch ({member.Name}TypeId)");
            }
            
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
}