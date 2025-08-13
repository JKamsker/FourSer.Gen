using Microsoft.CodeAnalysis;
using Serializer.Generator.Helpers;
using Serializer.Generator.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Serializer.Generator;

internal static class TypeInfoProvider
{
    private static readonly SymbolDisplayFormat s_typeNameFormat = new
    (
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                              | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public static TypeToGenerate? GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        // We only generate for top-level types. Nested types are generated as part of their container.
        // This preserves the original logic's behavior.
        if (typeSymbol.ContainingType != null)
        {
            return null;
        }

        var serializableMembers = GetSerializableMembers(typeSymbol);
        var nestedTypes = GetNestedTypes(typeSymbol);

        return new TypeToGenerate
        (
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.IsValueType,
            serializableMembers,
            nestedTypes
        );
    }

    private static EquatableArray<MemberToGenerate> GetSerializableMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<MemberToGenerate>();
        var currentType = typeSymbol;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var typeMembers = currentType.GetMembers()
                .Where
                (
                    m => !m.IsImplicitlyDeclared &&
                         (m is IPropertySymbol { SetMethod: not null } ||
                          m is IFieldSymbol { IsReadOnly: false, DeclaredAccessibility: Accessibility.Public })
                )
                .OrderBy(m => m.Locations.First().SourceSpan.Start)
                .Select
                (
                    m =>
                    {
                        var memberTypeSymbol = m is IPropertySymbol p ? p.Type : ((IFieldSymbol)m).Type;
                        var isList = memberTypeSymbol.OriginalDefinition.ToDisplayString()
                                     == "System.Collections.Generic.List<T>";
                        ListTypeArgumentInfo? listTypeArgumentInfo = null;
                        if (isList)
                        {
                            var typeArgumentSymbol = ((INamedTypeSymbol)memberTypeSymbol).TypeArguments[0];
                            listTypeArgumentInfo = new ListTypeArgumentInfo
                            (
                                typeArgumentSymbol.ToDisplayString(s_typeNameFormat),
                                typeArgumentSymbol.IsUnmanagedType,
                                typeArgumentSymbol.SpecialType == SpecialType.System_String,
                                typeArgumentSymbol.GetAttributes()
                                    .Any
                                    (
                                        ad => ad.AttributeClass?.ToDisplayString()
                                              == "Serializer.Contracts.GenerateSerializerAttribute"
                                    )
                            );
                        }

                        var polymorphicInfo = GetPolymorphicInfo(m);

                        return new MemberToGenerate
                        (
                            m.Name,
                            memberTypeSymbol.ToDisplayString(s_typeNameFormat),
                            memberTypeSymbol.IsUnmanagedType,
                            memberTypeSymbol.SpecialType == SpecialType.System_String,
                            memberTypeSymbol.GetAttributes()
                                .Any
                                (
                                    ad => ad.AttributeClass?.ToDisplayString()
                                          == "Serializer.Contracts.GenerateSerializerAttribute"
                                ),
                            isList,
                            listTypeArgumentInfo,
                            GetCollectionInfo(m),
                            polymorphicInfo
                        );
                    }
                )
                .ToList();

            members.InsertRange(0, typeMembers);
            currentType = currentType.BaseType;
        }

        return new EquatableArray<MemberToGenerate>(members.ToImmutableArray());
    }

    private static EquatableArray<TypeToGenerate> GetNestedTypes(INamedTypeSymbol parentType)
    {
        var nestedTypes = ImmutableArray.CreateBuilder<TypeToGenerate>();

        foreach (var member in parentType.GetMembers())
        {
            if (member is INamedTypeSymbol nestedTypeSymbol &&
                nestedTypeSymbol.GetAttributes()
                    .Any(ad => ad.AttributeClass?.ToDisplayString() == "Serializer.Contracts.GenerateSerializerAttribute"))
            {
                var nestedMembers = GetSerializableMembers(nestedTypeSymbol);
                var deeperNestedTypes = GetNestedTypes(nestedTypeSymbol);

                nestedTypes.Add
                (
                    new TypeToGenerate
                    (
                        nestedTypeSymbol.Name,
                        nestedTypeSymbol.ContainingNamespace.ToDisplayString(),
                        nestedTypeSymbol.IsValueType,
                        nestedMembers,
                        deeperNestedTypes
                    )
                );
            }
        }

        return new EquatableArray<TypeToGenerate>(nestedTypes.ToImmutable());
    }

    private static CollectionInfo? GetCollectionInfo(ISymbol member)
    {
        var attribute = AttributeHelper.GetCollectionAttribute(member);
        if (attribute is null) return null;

        var polymorphicMode = (PolymorphicMode)AttributeHelper.GetPolymorphicMode(attribute);
        var typeIdProperty = AttributeHelper.GetCollectionTypeIdProperty(attribute);
        var countType = AttributeHelper.GetCountType(attribute)?.ToDisplayString(s_typeNameFormat);
        var countSize = AttributeHelper.GetCountSize(attribute);
        var countSizeReference = AttributeHelper.GetCountSizeReference(attribute);

        return new CollectionInfo
        (
            polymorphicMode,
            typeIdProperty,
            countType,
            countSize,
            countSizeReference
        );
    }

    private static PolymorphicInfo? GetPolymorphicInfo(ISymbol member)
    {
        var attribute = AttributeHelper.GetPolymorphicAttribute(member);
        var collectionAttribute = AttributeHelper.GetCollectionAttribute(member);
        var options = AttributeHelper.GetPolymorphicOptions(member);

        // Only create PolymorphicInfo if there are actual polymorphic options or explicit polymorphic configuration
        var hasPolymorphicOptions = options.Any();
        var hasPolymorphicAttribute = attribute is not null;
        var hasPolymorphicCollectionMode = collectionAttribute is not null && 
            AttributeHelper.GetPolymorphicMode(collectionAttribute) != 0; // 0 = PolymorphicMode.None

        if (!hasPolymorphicOptions && !hasPolymorphicAttribute && !hasPolymorphicCollectionMode)
        {
            return null;
        }

        var typeIdProperty = AttributeHelper.GetTypeIdProperty(attribute) ?? AttributeHelper.GetCollectionTypeIdProperty
            (collectionAttribute);
        var typeIdType = AttributeHelper.GetTypeIdType(attribute) ?? AttributeHelper.GetCollectionTypeIdType(collectionAttribute);

        var polymorphicOptions = options.Select
            (
                optionAttribute =>
                {
                    var (key, type) = AttributeHelper.GetPolymorphicOption(optionAttribute);
                    return new PolymorphicOption(key, type.ToDisplayString());
                }
            )
            .ToImmutableArray();

        var enumUnderlyingType = typeIdType is { TypeKind: TypeKind.Enum }
            ? ((INamedTypeSymbol)typeIdType).EnumUnderlyingType!.ToDisplayString()
            : null;

        return new PolymorphicInfo
        (
            typeIdProperty,
            typeIdType?.ToDisplayString() ?? "int",
            new EquatableArray<PolymorphicOption>(polymorphicOptions),
            enumUnderlyingType
        );
    }
}