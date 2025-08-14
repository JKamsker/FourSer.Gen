using Microsoft.CodeAnalysis;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace FourSer.Gen;

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

        var hasSerializableBaseType = typeSymbol.BaseType?.GetAttributes()
            .Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute") ?? false;

        return new TypeToGenerate
        (
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.IsValueType,
            serializableMembers,
            nestedTypes,
            hasSerializableBaseType
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
                                              == "FourSer.Contracts.GenerateSerializerAttribute"
                                    )
                            );
                        }

                        var (isCollection, collectionTypeInfo) = GetCollectionTypeInfo(memberTypeSymbol);

                        var polymorphicInfo = GetPolymorphicInfo(m);
                        var friendlyTypeName = GetMethodFriendlyTypeName(memberTypeSymbol.ToDisplayString(s_typeNameFormat));

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
                                          == "FourSer.Contracts.GenerateSerializerAttribute"
                                ),
                            isList,
                            listTypeArgumentInfo,
                            GetCollectionInfo(m),
                            polymorphicInfo,
                            isCollection,
                            collectionTypeInfo,
                            memberTypeSymbol.IsUnmanagedType ? $"Write{friendlyTypeName}" : null,
                            memberTypeSymbol.IsUnmanagedType ? $"Read{friendlyTypeName}" : null
                        );
                    }
                )
                .ToList();

            members.InsertRange(0, typeMembers);
            currentType = currentType.BaseType;
        }

        return new EquatableArray<MemberToGenerate>(members.ToImmutableArray());
    }

    private static string GetMethodFriendlyTypeName(string typeName)
    {
        return typeName switch
        {
            "int" => "Int32",
            "uint" => "UInt32",
            "short" => "Int16",
            "ushort" => "UInt16",
            "long" => "Int64",
            "ulong" => "UInt64",
            "byte" => "Byte",
            "sbyte" => "SByte",
            "float" => "Single",
            "bool" => "Boolean",
            "double" => "Double",
            "char" => "Char",
            "string" => "String",
            _ => TypeHelper.GetMethodFriendlyTypeName(typeName)
        };
    }

    private static EquatableArray<TypeToGenerate> GetNestedTypes(INamedTypeSymbol parentType)
    {
        var nestedTypes = ImmutableArray.CreateBuilder<TypeToGenerate>();

        foreach (var member in parentType.GetMembers())
        {
            if (member is INamedTypeSymbol nestedTypeSymbol &&
                nestedTypeSymbol.GetAttributes()
                    .Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute"))
            {
                var nestedMembers = GetSerializableMembers(nestedTypeSymbol);
                var deeperNestedTypes = GetNestedTypes(nestedTypeSymbol);

                var hasSerializableBaseType = nestedTypeSymbol.BaseType?.GetAttributes()
                    .Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute") ?? false;

                nestedTypes.Add
                (
                    new TypeToGenerate
                    (
                        nestedTypeSymbol.Name,
                        nestedTypeSymbol.ContainingNamespace.ToDisplayString(),
                        nestedTypeSymbol.IsValueType,
                        nestedMembers,
                        deeperNestedTypes,
                        hasSerializableBaseType
                    )
                );
            }
        }

        return new EquatableArray<TypeToGenerate>(nestedTypes.ToImmutable());
    }

    private static (bool IsCollection, CollectionTypeInfo? CollectionTypeInfo) GetCollectionTypeInfo(ITypeSymbol typeSymbol)
    {
        string elementTypeName;
        bool isArray = false;
        string countAccessExpression;
        string concreteTypeInstantiation;
        string addMethodName;
        string originalDefinition;

        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            isArray = true;
            var elementType = arrayTypeSymbol.ElementType;
            elementTypeName = elementType.ToDisplayString(s_typeNameFormat);
            countAccessExpression = "Length";
            concreteTypeInstantiation = $"new {elementTypeName}[count]";
            addMethodName = ""; // Not applicable for arrays, assignment is by index
            originalDefinition = typeSymbol.ToDisplayString(s_typeNameFormat);
        }
        else if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType && namedTypeSymbol.TypeArguments.Length == 1)
        {
            var elementType = namedTypeSymbol.TypeArguments[0];
            elementTypeName = elementType.ToDisplayString(s_typeNameFormat);
            originalDefinition = namedTypeSymbol.OriginalDefinition.ToDisplayString();

            switch (originalDefinition)
            {
                case "System.Collections.Generic.List<T>":
                case "System.Collections.Generic.IList<T>":
                case "System.Collections.Generic.ICollection<T>":
                    countAccessExpression = "Count";
                    concreteTypeInstantiation = $"new global::System.Collections.Generic.List<{elementTypeName}>()";
                    addMethodName = "Add";
                    break;
                case "System.Collections.Generic.Queue<T>":
                    countAccessExpression = "Count";
                    concreteTypeInstantiation = $"new global::System.Collections.Generic.Queue<{elementTypeName}>()";
                    addMethodName = "Enqueue";
                    break;
                case "System.Collections.Generic.Stack<T>":
                    countAccessExpression = "Count";
                    concreteTypeInstantiation = $"new global::System.Collections.Generic.Stack<{elementTypeName}>()";
                    addMethodName = "Push";
                    break;
                case "System.Collections.Generic.HashSet<T>":
                    countAccessExpression = "Count";
                    concreteTypeInstantiation = $"new global::System.Collections.Generic.HashSet<{elementTypeName}>()";
                    addMethodName = "Add";
                    break;
                case "System.Collections.Generic.LinkedList<T>":
                    countAccessExpression = "Count";
                    concreteTypeInstantiation = $"new global::System.Collections.Generic.LinkedList<{elementTypeName}>()";
                    addMethodName = "AddLast";
                    break;
                case "System.Collections.Concurrent.ConcurrentBag<T>":
                    countAccessExpression = "Count";
                    concreteTypeInstantiation = $"new global::System.Collections.Concurrent.ConcurrentBag<{elementTypeName}>()";
                    addMethodName = "Add";
                    break;
                default:
                    return (false, null);
            }
        }
        else
        {
            return (false, null);
        }

        var genericElementType = (typeSymbol as IArrayTypeSymbol)?.ElementType ?? ((INamedTypeSymbol)typeSymbol).TypeArguments[0];

        return (true, new CollectionTypeInfo(
            originalDefinition,
            elementTypeName,
            genericElementType.IsUnmanagedType,
            genericElementType.SpecialType == SpecialType.System_String,
            genericElementType.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute"),
            isArray,
            countAccessExpression,
            concreteTypeInstantiation,
            addMethodName
        ));
    }

    private static CollectionInfo? GetCollectionInfo(ISymbol member)
    {
        var attribute = AttributeHelper.GetCollectionAttribute(member);
        
        // Check if this member is any supported collection type
        var memberTypeSymbol = member is IPropertySymbol p ? p.Type : ((IFieldSymbol)member).Type;
        var (isCollection, _) = GetCollectionTypeInfo(memberTypeSymbol);
        
        // If it's a collection but has no attribute, provide default collection info
        if (attribute is null)
        {
            if (isCollection)
            {
                // Return default CollectionInfo with no special configuration
                return new CollectionInfo
                (
                    PolymorphicMode.None,
                    null, // TypeIdProperty
                    null, // CountType (will use default)
                    null, // CountSize (will use default)
                    null  // CountSizeReference
                );
            }
            return null;
        }

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