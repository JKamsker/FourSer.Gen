using System;
using Microsoft.CodeAnalysis;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            serializableMembers,
            nestedTypes,
            hasSerializableBaseType,
            constructorInfo
        );
    }

    private static ConstructorInfo? GetConstructorInfo(INamedTypeSymbol typeSymbol,
        EquatableArray<MemberToGenerate> members)
    {
        var constructors = new List<IMethodSymbol>();
        foreach (var constructor in typeSymbol.Constructors)
        {
            if (!constructor.IsImplicitlyDeclared)
            {
                constructors.Add(constructor);
            }
        }

        bool hasParameterlessCtor = false;
        foreach (var constructor in constructors)
        {
            if (constructor.Parameters.Length == 0)
            {
                hasParameterlessCtor = true;
                break;
            }
        }

        bool hasReadOnlyMembers = false;
        foreach (var member in members)
        {
            if (member.IsReadOnly)
            {
                hasReadOnlyMembers = true;
                break;
            }
        }

        if (!hasReadOnlyMembers)
        {
            var publicConstructors = new List<IMethodSymbol>();
            foreach (var constructor in constructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public)
                {
                    publicConstructors.Add(constructor);
                }
            }

            if (publicConstructors.Count > 0)
            {
                var bestConstructor = FindBestConstructor(publicConstructors, members);
                if (bestConstructor is not null)
                {
                    var parametersBuilder = ImmutableArray.CreateBuilder<ParameterInfo>(bestConstructor.Parameters.Length);
                    foreach (var p in bestConstructor.Parameters)
                    {
                        parametersBuilder.Add(new ParameterInfo(p.Name, p.Type.ToDisplayString(s_typeNameFormat)));
                    }

                    return new ConstructorInfo(new EquatableArray<ParameterInfo>(parametersBuilder.ToImmutable()), false,
                        hasParameterlessCtor);
                }
            }
        }

        var generatedParametersBuilder = ImmutableArray.CreateBuilder<ParameterInfo>(members.Count);
        foreach (var m in members)
        {
            generatedParametersBuilder.Add(new ParameterInfo(m.Name, m.TypeName));
        }

        return new ConstructorInfo(new EquatableArray<ParameterInfo>(generatedParametersBuilder.ToImmutable()), true,
            hasParameterlessCtor);
    }

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors,
        EquatableArray<MemberToGenerate> members)
    {
        if (members.Count == 0)
        {
            foreach (var constructor in constructors)
            {
                if (constructor.Parameters.Length == 0)
                {
                    return constructor;
                }
            }
            return null;
        }

        var memberDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            memberDict[member.Name] = member.TypeName;
        }

        foreach (var c in constructors)
        {
            if (c.Parameters.Length != members.Count)
            {
                continue;
            }

            if (AllParametersMatch(c, memberDict))
            {
                return c;
            }
        }

        return null;
    }

    private static bool AllParametersMatch(IMethodSymbol constructor, IReadOnlyDictionary<string, string> members)
    {
        foreach (var p in constructor.Parameters)
        {
            if (!members.TryGetValue(p.Name, out var typeName) ||
                typeName != p.Type.ToDisplayString(s_typeNameFormat))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSerializableMember(ISymbol member, IReadOnlyCollection<ISymbol> propertiesWithBackingFields)
    {
        if (member.IsImplicitlyDeclared || member.IsStatic)
        {
            return false;
        }

        switch (member)
        {
            case IPropertySymbol p:
            {
                if (p.IsIndexer || p.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }

                if (p.SetMethod is not null)
                {
                    return true;
                }

                foreach (var symbol in propertiesWithBackingFields)
                {
                    if (SymbolEqualityComparer.Default.Equals(symbol, p))
                    {
                        return true;
                    }
                }
                return false;
            }
            case IFieldSymbol field:
                return field.AssociatedSymbol is null && field.DeclaredAccessibility == Accessibility.Public;
            default:
                return false;
        }
    }

    private static EquatableArray<MemberToGenerate> GetSerializableMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<MemberToGenerate>();
        var currentType = typeSymbol;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var membersInType = currentType.GetMembers();

            var propertiesWithBackingFieldsBuilder = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var member in membersInType)
            {
                if (member is IFieldSymbol { AssociatedSymbol: not null } f)
                {
                    propertiesWithBackingFieldsBuilder.Add(f.AssociatedSymbol!);
                }
            }
            var propertiesWithBackingFields = propertiesWithBackingFieldsBuilder.ToImmutable();

            var typeMembersWithLocation = new List<(MemberToGenerate, Location)>();
            foreach (var m in membersInType)
            {
                if (IsSerializableMember(m, propertiesWithBackingFields))
                {
                    typeMembersWithLocation.Add(CreateMemberToGenerate(m));
                }
            }

            typeMembersWithLocation.Sort((x, y) => x.Item2.SourceSpan.Start.CompareTo(y.Item2.SourceSpan.Start));

            var sortedMembers = new List<MemberToGenerate>(typeMembersWithLocation.Count);
            foreach (var m in typeMembersWithLocation)
            {
                sortedMembers.Add(m.Item1);
            }

            members.InsertRange(0, sortedMembers);
            currentType = currentType.BaseType;
        }

        return new EquatableArray<MemberToGenerate>(members.ToImmutableArray());
    }

    private static (MemberToGenerate, Location) CreateMemberToGenerate(ISymbol m)
    {
        var memberTypeSymbol = m is IPropertySymbol p ? p.Type : ((IFieldSymbol)m).Type;
        var isList = memberTypeSymbol.OriginalDefinition.ToDisplayString()
                     == "System.Collections.Generic.List<T>";
        ListTypeArgumentInfo? listTypeArgumentInfo = null;
        if (isList)
        {
            var typeArgumentSymbol = ((INamedTypeSymbol)memberTypeSymbol).TypeArguments[0];
            var listTypeHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(typeArgumentSymbol as INamedTypeSymbol);

            listTypeArgumentInfo = new ListTypeArgumentInfo
            (
                typeArgumentSymbol.ToDisplayString(s_typeNameFormat),
                typeArgumentSymbol.IsUnmanagedType,
                typeArgumentSymbol.SpecialType == SpecialType.System_String,
                listTypeHasGenerateSerializerAttribute
            );
        }

        var (isCollection, collectionTypeInfo) = GetCollectionTypeInfo(memberTypeSymbol);

        var polymorphicInfo = GetPolymorphicInfo(m);

        bool isReadOnly = false;
        if (m is IPropertySymbol prop)
        {
            isReadOnly = prop.SetMethod is null;
        }
        else if (m is IFieldSymbol field)
        {
            isReadOnly = field.IsReadOnly;
        }

        var memberHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(memberTypeSymbol as INamedTypeSymbol);

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
            isReadOnly
        );

        return (memberToGenerate, m.Locations[0]);
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
                    nestedTypes.Add(nestedType.Value);
                }
            }
        }

        return new EquatableArray<TypeToGenerate>(nestedTypes.ToImmutable());
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
        bool isCollection = false;

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
            new EquatableArray<PolymorphicOption>(polymorphicOptions),
            enumUnderlyingType
        );
    }

    private static ImmutableArray<PolymorphicOption> GetPolymorphicOptions(List<AttributeData> options)
    {
        var polymorphicOptionsBuilder = ImmutableArray.CreateBuilder<PolymorphicOption>();
        foreach (var optionAttribute in options)
        {
            var (key, type) = AttributeHelper.GetPolymorphicOption(optionAttribute);
            polymorphicOptionsBuilder.Add(new PolymorphicOption(key, type.ToDisplayString()));
        }
        return polymorphicOptionsBuilder.ToImmutable();
    }
}