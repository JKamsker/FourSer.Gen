using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen;

internal static class TypeInfoProvider
{
    private static readonly SymbolDisplayFormat s_typeNameFormat = new
    (
        SymbolDisplayGlobalNamespaceStyle.Omitted,
        SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public static TypeToGenerate? GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        if (typeSymbol.ContainingType != null)
        {
            return null;
        }

        var serializableMembers = GetSerializableMembers(typeSymbol);
        var nestedTypes = GetNestedTypes(typeSymbol);
        var constructorInfo = GetConstructorInfo(typeSymbol, serializableMembers);

        var hasSerializableBaseType = HasGenerateSerializerAttribute(typeSymbol.BaseType);

        return new TypeToGenerate
        (
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.IsValueType,
            typeSymbol.IsRecord,
            serializableMembers,
            nestedTypes,
            hasSerializableBaseType,
            constructorInfo
        );
    }

    private static ConstructorInfo? GetConstructorInfo
    (
        INamedTypeSymbol typeSymbol,
        EquatableArray<MemberToGenerate> members
    )
    {
        var constructors = new List<IMethodSymbol>();
        foreach (var c in typeSymbol.Constructors)
        {
            if (!c.IsImplicitlyDeclared)
            {
                constructors.Add(c);
            }
        }

        var hasParameterlessCtor = HasParameterlessConstructor(constructors);
        var shouldGenerate = HasReadOnlyMembers(members);

        if (!shouldGenerate)
        {
            var publicConstructors = GetPublicConstructors(constructors);

            if (publicConstructors.Count > 0)
            {
                var bestConstructor = FindBestConstructor(publicConstructors, members);
                if (bestConstructor is not null)
                {
                    var parameters = ImmutableArray.CreateBuilder<ParameterInfo>();
                    foreach (var p in bestConstructor.Parameters)
                    {
                        parameters.Add(new(p.Name, p.Type.ToDisplayString(s_typeNameFormat)));
                    }

                    return new ConstructorInfo(new(parameters.ToImmutable()), false, hasParameterlessCtor);
                }
            }
        }

        var generatedParametersBuilder = ImmutableArray.CreateBuilder<ParameterInfo>();
        foreach (var m in members)
        {
            generatedParametersBuilder.Add(new(m.Name, m.TypeName));
        }

        return new ConstructorInfo(new(generatedParametersBuilder.ToImmutable()), true, hasParameterlessCtor);
    }

    private static bool HasParameterlessConstructor(List<IMethodSymbol> constructors)
    {
        foreach (var c in constructors)
        {
            if (c.Parameters.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBackingField(IPropertySymbol p)
    {
        foreach (var innerMember in p.ContainingType.GetMembers())
        {
            if (innerMember is IFieldSymbol f && SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, p))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReadOnlyMembers(EquatableArray<MemberToGenerate> members)
    {
        foreach (var m in members)
        {
            if (m.IsReadOnly || m.IsInitOnly)
            {
                return true;
            }
        }

        return false;
    }

    private static List<IMethodSymbol> GetPublicConstructors(List<IMethodSymbol> constructors)
    {
        var publicConstructors = new List<IMethodSymbol>();
        foreach (var c in constructors)
        {
            if (c.DeclaredAccessibility == Accessibility.Public)
            {
                publicConstructors.Add(c);
            }
        }

        return publicConstructors;
    }

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors, EquatableArray<MemberToGenerate> members)
    {
        foreach (var c in constructors)
        {
            var membersCount = 0;
            foreach (var _ in members)
            {
                membersCount++;
            }

            if (c.Parameters.Length != membersCount)
            {
                continue;
            }

            if (AllParametersMatch(c, members))
            {
                return c;
            }
        }

        return null;
    }

    private static bool AllParametersMatch(IMethodSymbol constructor, EquatableArray<MemberToGenerate> members)
    {
        foreach (var p in constructor.Parameters)
        {
            var parameterMatches = false;
            foreach (var m in members)
            {
                if (string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase) &&
                    m.TypeName == p.Type.ToDisplayString(s_typeNameFormat))
                {
                    parameterMatches = true;
                    break;
                }
            }

            if (!parameterMatches)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSerializableMember(ISymbol member)
    {
        if (member.IsImplicitlyDeclared || member.IsStatic)
        {
            return false;
        }

        if (member is IPropertySymbol p)
        {
            if (p.IsIndexer || p.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            // Properties with a setter are serializable.
            if (p.SetMethod is not null)
            {
                return true;
            }

            // Read-only properties are serializable if they are auto-properties (have a backing field).
            return HasBackingField(p);
        }

        if (member is IFieldSymbol field)
        {
            return field.DeclaredAccessibility == Accessibility.Public;
        }

        return false;
    }

    private static EquatableArray<MemberToGenerate> GetSerializableMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<MemberToGenerate>();
        var currentType = typeSymbol;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var typeMembersWithLocation = new List<(MemberToGenerate, Location)>();
            foreach (var m in currentType.GetMembers())
            {
                if (IsSerializableMember(m))
                {
                    typeMembersWithLocation.Add(CreateMemberToGenerate(m));
                }
            }

            typeMembersWithLocation.Sort((m1, m2) => m1.Item2.SourceSpan.Start.CompareTo(m2.Item2.SourceSpan.Start));

            var typeMembers = new List<MemberToGenerate>();
            foreach (var m in typeMembersWithLocation)
            {
                typeMembers.Add(m.Item1);
            }

            members.InsertRange(0, typeMembers);
            currentType = currentType.BaseType;
        }

        return new(members.ToImmutableArray());
    }

    private static (MemberToGenerate, Location) CreateMemberToGenerate(ISymbol m)
    {
        var memberTypeSymbol = m is IPropertySymbol p ? p.Type : ((IFieldSymbol)m).Type;
        var (isCollection, collectionTypeInfo) = GetCollectionTypeInfo(memberTypeSymbol);
        var isList = memberTypeSymbol.OriginalDefinition.ToDisplayString()
                     == "System.Collections.Generic.List<T>";
        ListTypeArgumentInfo? listTypeArgumentInfo = null;
        if (isCollection && collectionTypeInfo.HasValue)
        {
            var cti = collectionTypeInfo.Value;
            listTypeArgumentInfo = new ListTypeArgumentInfo
            (
                cti.ElementTypeName,
                cti.IsElementUnmanagedType,
                cti.IsElementStringType,
                cti.HasElementGenerateSerializerAttribute
            );
        }

        var polymorphicInfo = GetPolymorphicInfo(m);

        var isReadOnly = false;
        var isInitOnly = false;
        if (m is IPropertySymbol prop)
        {
            isReadOnly = prop.SetMethod is null;
            isInitOnly = prop.SetMethod?.IsInitOnly ?? false;
        }
        else if (m is IFieldSymbol field)
        {
            isReadOnly = field.IsReadOnly;
        }

        var memberHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(memberTypeSymbol as INamedTypeSymbol);

        var location = m.Locations.First();
        var lineSpan = location.GetLineSpan();
        var locationInfo = new LocationInfo(lineSpan.Path, lineSpan.StartLinePosition.Line, lineSpan.EndLinePosition.Line);

        var memberToGenerate = new MemberToGenerate
        (
            m.Name,
            memberTypeSymbol.ToDisplayString(s_typeNameFormat),
            memberTypeSymbol.IsUnmanagedType,
            memberTypeSymbol.SpecialType == SpecialType.System_String,
            memberHasGenerateSerializerAttribute,
            isList,
            listTypeArgumentInfo,
            GetCollectionInfo(m),
            polymorphicInfo,
            isCollection,
            collectionTypeInfo,
            isReadOnly,
            isInitOnly,
            locationInfo
        );

        return (memberToGenerate, location);
    }

    private static EquatableArray<TypeToGenerate> GetNestedTypes(INamedTypeSymbol parentType)
    {
        var nestedTypes = ImmutableArray.CreateBuilder<TypeToGenerate>();

        foreach (var member in parentType.GetMembers())
        {
            if (member is INamedTypeSymbol nestedTypeSymbol)
            {
                var nestedType = CreateNestedTypeToGenerate(nestedTypeSymbol);
                if (nestedType is not null)
                {
                    nestedTypes.Add(nestedType);
                }
            }
        }

        return new(nestedTypes.ToImmutable());
    }

    private static bool HasGenerateSerializerAttribute(INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        foreach (var ad in typeSymbol.GetAttributes())
        {
            if (ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static TypeToGenerate? CreateNestedTypeToGenerate(INamedTypeSymbol nestedTypeSymbol)
    {
        if (!HasGenerateSerializerAttribute(nestedTypeSymbol))
        {
            return null;
        }

        var nestedMembers = GetSerializableMembers(nestedTypeSymbol);
        var deeperNestedTypes = GetNestedTypes(nestedTypeSymbol);

        var hasSerializableBaseType = HasGenerateSerializerAttribute(nestedTypeSymbol.BaseType);

        var constructorInfo = GetConstructorInfo(nestedTypeSymbol, nestedMembers);

        return new TypeToGenerate
        (
            nestedTypeSymbol.Name,
            nestedTypeSymbol.ContainingNamespace.ToDisplayString(),
            nestedTypeSymbol.IsValueType,
            nestedTypeSymbol.IsRecord,
            nestedMembers,
            deeperNestedTypes,
            hasSerializableBaseType,
            constructorInfo
        );
    }

    private static (bool IsCollection, CollectionTypeInfo? CollectionTypeInfo) GetCollectionTypeInfo(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            var elementType = arrayTypeSymbol.ElementType;
            var arrayElementHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(elementType as INamedTypeSymbol);
            return (true, new CollectionTypeInfo
            (
                typeSymbol.ToDisplayString(s_typeNameFormat),
                elementType.ToDisplayString(s_typeNameFormat),
                elementType.IsUnmanagedType,
                elementType.SpecialType == SpecialType.System_String,
                arrayElementHasGenerateSerializerAttribute,
                true,
                null
            ));
        }

        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return (false, null);
        }

        if (!namedTypeSymbol.IsGenericType || namedTypeSymbol.TypeArguments.Length != 1)
        {
            return (false, null);
        }

        var originalDefinition = namedTypeSymbol.OriginalDefinition.ToDisplayString();
        var genericElementType = namedTypeSymbol.TypeArguments[0];

        string? concreteTypeName = null;
        var isCollection = false;

        switch (originalDefinition)
        {
            case "System.Collections.Generic.List<T>":
                isCollection = true;
                concreteTypeName = null;
                break;
            case "System.Collections.Generic.IList<T>":
            case "System.Collections.Generic.ICollection<T>":
            case "System.Collections.Generic.IEnumerable<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.Generic.List";
                break;
            case "System.Collections.ObjectModel.Collection<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.ObjectModel.Collection";
                break;
            case "System.Collections.ObjectModel.ObservableCollection<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.ObjectModel.ObservableCollection";
                break;
            case "System.Collections.Generic.HashSet<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.Generic.HashSet";
                break;
            case "System.Collections.Generic.SortedSet<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.Generic.SortedSet";
                break;
            case "System.Collections.Generic.Queue<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.Generic.Queue";
                break;
            case "System.Collections.Generic.Stack<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.Generic.Stack";
                break;
            case "System.Collections.Generic.LinkedList<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.Generic.LinkedList";
                break;
            case "System.Collections.Concurrent.ConcurrentBag<T>":
                isCollection = true;
                concreteTypeName = "System.Collections.Concurrent.ConcurrentBag";
                break;
        }

        if (!isCollection)
        {
            return (false, null);
        }

        var hasGenerateSerializerAttribute = HasGenerateSerializerAttribute(genericElementType as INamedTypeSymbol);

        return (true, new CollectionTypeInfo
        (
            originalDefinition,
            genericElementType.ToDisplayString(s_typeNameFormat),
            genericElementType.IsUnmanagedType,
            genericElementType.SpecialType == SpecialType.System_String,
            hasGenerateSerializerAttribute,
            false,
            concreteTypeName
        ));
    }

    private static CollectionInfo? GetCollectionInfo(ISymbol member)
    {
        var attribute = AttributeHelper.GetCollectionAttribute(member);

        var memberTypeSymbol = member is IPropertySymbol p ? p.Type : ((IFieldSymbol)member).Type;
        var (isCollection, _) = GetCollectionTypeInfo(memberTypeSymbol);

        if (attribute is null)
        {
            if (isCollection)
            {
                return new CollectionInfo
                (
                    PolymorphicMode.None,
                    null,
                    null,
                    null,
                    null
                );
            }

            return null;
        }

        var polymorphicMode = (PolymorphicMode)AttributeHelper.GetPolymorphicMode(attribute);
        var typeIdProperty = AttributeHelper.GetCollectionTypeIdProperty(attribute);

        if (!string.IsNullOrEmpty(typeIdProperty) && polymorphicMode == PolymorphicMode.None)
        {
            polymorphicMode = PolymorphicMode.SingleTypeId;
        }

        var countType = AttributeHelper.GetCountType(attribute)?.ToDisplayString(s_typeNameFormat);
        var countSize = AttributeHelper.GetCountSize(attribute);
        var countSizeReference = AttributeHelper.GetCountSizeReference(attribute);
        var unlimited = AttributeHelper.GetUnlimited(attribute);

        return new CollectionInfo
        (
            polymorphicMode,
            typeIdProperty,
            countType,
            countSize,
            countSizeReference,
            unlimited
        );
    }

    private static PolymorphicInfo? GetPolymorphicInfo(ISymbol member)
    {
        var attribute = AttributeHelper.GetPolymorphicAttribute(member);
        var collectionAttribute = AttributeHelper.GetCollectionAttribute(member);
        var options = AttributeHelper.GetPolymorphicOptions(member);

        var hasPolymorphicOptions = options.Count > 0;
        var hasPolymorphicAttribute = attribute is not null;
        var hasPolymorphicCollectionMode = collectionAttribute is not null &&
            AttributeHelper.GetPolymorphicMode(collectionAttribute) != 0;

        if (!hasPolymorphicOptions && !hasPolymorphicAttribute && !hasPolymorphicCollectionMode)
        {
            return null;
        }

        var typeIdProperty = AttributeHelper.GetTypeIdProperty(attribute) ?? AttributeHelper.GetCollectionTypeIdProperty
            (collectionAttribute);
        var typeIdType = AttributeHelper.GetTypeIdType(attribute) ?? AttributeHelper.GetCollectionTypeIdType(collectionAttribute);

        var polymorphicOptions = GetPolymorphicOptions(options);

        var enumUnderlyingType = typeIdType is { TypeKind: TypeKind.Enum }
            ? ((INamedTypeSymbol)typeIdType).EnumUnderlyingType!.ToDisplayString()
            : null;

        return new PolymorphicInfo
        (
            typeIdProperty,
            typeIdType?.ToDisplayString() ?? "int",
            new(polymorphicOptions),
            enumUnderlyingType
        );
    }

    private static ImmutableArray<PolymorphicOption> GetPolymorphicOptions(List<AttributeData> options)
    {
        var polymorphicOptionsBuilder = ImmutableArray.CreateBuilder<PolymorphicOption>();
        foreach (var optionAttribute in options)
        {
            var (key, type) = AttributeHelper.GetPolymorphicOption(optionAttribute);
            polymorphicOptionsBuilder.Add(new(key, type.ToDisplayString()));
        }

        return polymorphicOptionsBuilder.ToImmutable();
    }
}