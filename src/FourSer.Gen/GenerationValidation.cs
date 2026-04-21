using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen;

internal static class GenerationValidation
{
    public static bool HasInvalidConfiguration(
        SourceProductionContext context,
        TypeToGenerate typeToGenerate,
        DiagnosticDescriptor configurationErrorRule,
        string typeName)
    {
        return HasInvalidCollection(context, typeToGenerate, configurationErrorRule, typeName)
            || HasInvalidPolymorphicConfiguration(context, typeToGenerate, configurationErrorRule, typeName);
    }

    private static bool HasInvalidCollection(
        SourceProductionContext context,
        TypeToGenerate typeToGenerate,
        DiagnosticDescriptor configurationErrorRule,
        string typeName)
    {
        foreach (var member in typeToGenerate.Members)
        {
            if (member.IsMemoryOwner)
            {
                if (member.CollectionInfo?.Unlimited == true)
                {
                    ReportConfigurationError(
                        context,
                        configurationErrorRule,
                        typeName,
                        $"IMemoryOwner<T> member '{member.Name}' is marked as Unlimited, which is not supported.");
                    return true;
                }

                if (member.CustomSerializer is not null || member.MemoryOwnerTypeInfo is not { } memoryOwnerTypeInfo)
                {
                    continue;
                }

                if (memoryOwnerTypeInfo.IsElementUnmanagedType
                    || memoryOwnerTypeInfo.IsElementStringType
                    || memoryOwnerTypeInfo.HasElementGenerateSerializerAttribute)
                {
                    continue;
                }

                ReportConfigurationError(
                    context,
                    configurationErrorRule,
                    typeName,
                    $"IMemoryOwner<T> member '{member.Name}' has unsupported element type '{memoryOwnerTypeInfo.ElementTypeName}'. Add [GenerateSerializer] to the element type or apply [Serializer(...)] to the member.");
                return true;
            }

            if (!member.IsCollection || member.CollectionTypeInfo is not { } collectionTypeInfo || member.CustomSerializer is not null)
            {
                continue;
            }

            if (collectionTypeInfo.IsElementUnmanagedType
                || collectionTypeInfo.IsElementStringType
                || collectionTypeInfo.HasElementGenerateSerializerAttribute)
            {
                continue;
            }

            if (RequestsPolymorphism(member))
            {
                continue;
            }

            ReportConfigurationError(
                context,
                configurationErrorRule,
                typeName,
                $"Collection member '{member.Name}' has unsupported element type '{collectionTypeInfo.ElementTypeName}'. Add [GenerateSerializer] to the element type or apply [Serializer(...)] to the collection member.");
            return true;
        }

        return false;
    }

    private static bool HasInvalidPolymorphicConfiguration(
        SourceProductionContext context,
        TypeToGenerate typeToGenerate,
        DiagnosticDescriptor configurationErrorRule,
        string typeName)
    {
        foreach (var member in typeToGenerate.Members)
        {
            if (!RequestsPolymorphism(member))
            {
                continue;
            }

            if (member.IsMemoryOwner)
            {
                ReportConfigurationError(
                    context,
                    configurationErrorRule,
                    typeName,
                    $"IMemoryOwner<T> member '{member.Name}' does not support polymorphic serialization.");
                return true;
            }

            if (member.PolymorphicInfo is not { } polymorphicInfo || polymorphicInfo.Options.IsEmpty)
            {
                ReportConfigurationError(
                    context,
                    configurationErrorRule,
                    typeName,
                    $"Member '{member.Name}' is configured for polymorphic serialization but has no [PolymorphicOption]s.");
                return true;
            }

            foreach (var option in polymorphicInfo.Options)
            {
                if (option.IsSerializableType)
                {
                    continue;
                }

                ReportConfigurationError(
                    context,
                    configurationErrorRule,
                    typeName,
                    $"Polymorphic option type '{option.Type}' on member '{member.Name}' is not serializable. Add [GenerateSerializer] to the option type or implement ISerializable<T>.");
                return true;
            }
        }

        return false;
    }

    private static bool RequestsPolymorphism(MemberToGenerate member)
    {
        var collectionInfo = member.CollectionInfo;
        return (collectionInfo?.PolymorphicMode ?? PolymorphicMode.None) != PolymorphicMode.None
            || !string.IsNullOrEmpty(collectionInfo?.TypeIdProperty)
            || member.PolymorphicInfo is not null;
    }

    private static void ReportConfigurationError(
        SourceProductionContext context,
        DiagnosticDescriptor configurationErrorRule,
        string typeName,
        string message)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                configurationErrorRule,
                Location.None,
                typeName,
                message));
    }
}
