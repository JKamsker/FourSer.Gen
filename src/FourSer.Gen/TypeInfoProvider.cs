using System;
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

    private sealed class WellKnownTypes
    {
        public readonly INamedTypeSymbol? GenerateSerializerAttribute;
        public readonly INamedTypeSymbol? ListT;
        public readonly INamedTypeSymbol? IListT;
        public readonly INamedTypeSymbol? ICollectionT;
        public readonly INamedTypeSymbol? IEnumerableT;
        public readonly INamedTypeSymbol? CollectionT;
        public readonly INamedTypeSymbol? ObservableCollectionT;
        public readonly INamedTypeSymbol? HashSetT;
        public readonly INamedTypeSymbol? SortedSetT;
        public readonly INamedTypeSymbol? QueueT;
        public readonly INamedTypeSymbol? StackT;
        public readonly INamedTypeSymbol? LinkedListT;
        public readonly INamedTypeSymbol? ConcurrentBagT;

        public WellKnownTypes(Compilation compilation)
        {
            GenerateSerializerAttribute = compilation.GetTypeByMetadataName("FourSer.Contracts.GenerateSerializerAttribute");
            ListT = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
            IListT = compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
            ICollectionT = compilation.GetTypeByMetadataName("System.Collections.Generic.ICollection`1");
            IEnumerableT = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
            CollectionT = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.Collection`1");
            ObservableCollectionT = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ObservableCollection`1");
            HashSetT = compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1");
            SortedSetT = compilation.GetTypeByMetadataName("System.Collections.Generic.SortedSet`1");
            QueueT = compilation.GetTypeByMetadataName("System.Collections.Generic.Queue`1");
            StackT = compilation.GetTypeByMetadataName("System.Collections.Generic.Stack`1");
            LinkedListT = compilation.GetTypeByMetadataName("System.Collections.Generic.LinkedList`1");
            ConcurrentBagT = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentBag`1");
        }
    }

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

        var compilation = context.SemanticModel.Compilation;
        var wellKnownTypes = new WellKnownTypes(compilation);
        var serializableMembers = GetSerializableMembers(typeSymbol, wellKnownTypes);
        var nestedTypes = GetNestedTypes(typeSymbol, wellKnownTypes);
        var constructorInfo = GetConstructorInfo(typeSymbol, serializableMembers);

        var hasSerializableBaseType = HasGenerateSerializerAttribute(typeSymbol.BaseType, wellKnownTypes);

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
        var publicConstructors = new List<IMethodSymbol>();
        var hasParameterlessCtor = false;

        foreach (var c in typeSymbol.Constructors)
        {
            if (c.IsImplicitlyDeclared) continue;

            if (c.Parameters.Length == 0)
            {
                hasParameterlessCtor = true;
            }

            if (c.DeclaredAccessibility == Accessibility.Public)
            {
                publicConstructors.Add(c);
            }
        }

        var shouldGenerate = HasReadOnlyMembers(members);

        if (!shouldGenerate)
        {
            if (publicConstructors.Count > 0)
            {
                var bestConstructor = FindBestConstructor(publicConstructors, members);
                if (bestConstructor is not null)
                {
                    var parameters = ImmutableArray.CreateBuilder<ParameterInfo>();
                    foreach (var p in bestConstructor.Parameters)
                    {
                        parameters.Add(new ParameterInfo(p.Name, p.Type.ToDisplayString(s_typeNameFormat)));
                    }
                    return new ConstructorInfo(new EquatableArray<ParameterInfo>(parameters.ToImmutable()), false, hasParameterlessCtor);
                }
            }
        }

        var generatedParametersBuilder = ImmutableArray.CreateBuilder<ParameterInfo>();
        foreach (var m in members)
        {
            generatedParametersBuilder.Add(new ParameterInfo(m.Name, m.TypeName));
        }

        return new ConstructorInfo(new EquatableArray<ParameterInfo>(generatedParametersBuilder.ToImmutable()), true, hasParameterlessCtor);
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
            if (m.IsReadOnly)
            {
                return true;
            }
        }
        return false;
    }

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors, EquatableArray<MemberToGenerate> members)
    {
        foreach (var c in constructors)
        {
            if (c.Parameters.Length != members.Length)
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
            bool parameterMatches = false;
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

    private static EquatableArray<MemberToGenerate> GetSerializableMembers(INamedTypeSymbol typeSymbol, WellKnownTypes wellKnownTypes)
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
                    typeMembersWithLocation.Add(CreateMemberToGenerate(m, wellKnownTypes));
                }
            }

            typeMembersWithLocation.Sort((m1, m2) => m1.Item2.SourceSpan.Start.CompareTo(m2.Item2.SourceSpan.Start));

            var sortedMembers = new MemberToGenerate[typeMembersWithLocation.Count];
            for (var i = 0; i < typeMembersWithLocation.Count; i++)
            {
                sortedMembers[i] = typeMembersWithLocation[i].Item1;
            }

            members.InsertRange(0, sortedMembers);
            currentType = currentType.BaseType;
        }

        return new EquatableArray<MemberToGenerate>(members.ToImmutableArray());
    }

    private static (MemberToGenerate, Location) CreateMemberToGenerate(ISymbol m, WellKnownTypes wellKnownTypes)
    {
        var memberTypeSymbol = m is IPropertySymbol p ? p.Type : ((IFieldSymbol)m).Type;

        var isList = wellKnownTypes.ListT is not null && SymbolEqualityComparer.Default.Equals(memberTypeSymbol.OriginalDefinition, wellKnownTypes.ListT);

        ListTypeArgumentInfo? listTypeArgumentInfo = null;
        if (isList)
        {
            var typeArgumentSymbol = ((INamedTypeSymbol)memberTypeSymbol).TypeArguments[0];
            var listTypeHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(typeArgumentSymbol as INamedTypeSymbol, wellKnownTypes);

            listTypeArgumentInfo = new ListTypeArgumentInfo
            (
                typeArgumentSymbol.ToDisplayString(s_typeNameFormat),
                typeArgumentSymbol.IsUnmanagedType,
                typeArgumentSymbol.SpecialType == SpecialType.System_String,
                listTypeHasGenerateSerializerAttribute
            );
        }

        var (isCollection, collectionTypeInfo) = GetCollectionTypeInfo(memberTypeSymbol, wellKnownTypes);

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

        var memberHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(memberTypeSymbol as INamedTypeSymbol, wellKnownTypes);

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
            GetCollectionInfo(m, wellKnownTypes),
            polymorphicInfo,
            isCollection,
            collectionTypeInfo,
            isReadOnly,
            locationInfo
        );

        return (memberToGenerate, location);
    }

    private static EquatableArray<TypeToGenerate> GetNestedTypes(INamedTypeSymbol parentType, WellKnownTypes wellKnownTypes)
    {
        var nestedTypes = ImmutableArray.CreateBuilder<TypeToGenerate>();

        foreach (var member in parentType.GetMembers())
        {
            if (member is INamedTypeSymbol nestedTypeSymbol)
            {
                var nestedType = CreateNestedTypeToGenerate(nestedTypeSymbol, wellKnownTypes);
                if (nestedType is not null)
                {
                    nestedTypes.Add(nestedType.Value);
                }
            }
        }

        return new EquatableArray<TypeToGenerate>(nestedTypes.ToImmutable());
    }

    private static bool HasGenerateSerializerAttribute(INamedTypeSymbol? typeSymbol, WellKnownTypes wellKnownTypes)
    {
        if (typeSymbol is null || wellKnownTypes.GenerateSerializerAttribute is null)
        {
            return false;
        }

        foreach (var ad in typeSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(ad.AttributeClass, wellKnownTypes.GenerateSerializerAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static TypeToGenerate? CreateNestedTypeToGenerate(INamedTypeSymbol nestedTypeSymbol, WellKnownTypes wellKnownTypes)
    {
        if (!HasGenerateSerializerAttribute(nestedTypeSymbol, wellKnownTypes))
        {
            return null;
        }

        var nestedMembers = GetSerializableMembers(nestedTypeSymbol, wellKnownTypes);
        var deeperNestedTypes = GetNestedTypes(nestedTypeSymbol, wellKnownTypes);

        var hasSerializableBaseType = HasGenerateSerializerAttribute(nestedTypeSymbol.BaseType, wellKnownTypes);

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

    private static (bool IsCollection, CollectionTypeInfo? CollectionTypeInfo) GetCollectionTypeInfo(ITypeSymbol typeSymbol, WellKnownTypes wellKnownTypes)
    {
        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            var elementType = arrayTypeSymbol.ElementType;
            var arrayElementHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(elementType as INamedTypeSymbol, wellKnownTypes);
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

        var originalDefinition = namedTypeSymbol.OriginalDefinition;
        var genericElementType = namedTypeSymbol.TypeArguments[0];

        string? concreteTypeName = null;
        bool isCollection = false;

        if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.ListT))
        {
            isCollection = true;
            concreteTypeName = null;
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.IListT) ||
                 SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.ICollectionT) ||
                 SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.IEnumerableT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.List";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.CollectionT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.ObjectModel.Collection";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.ObservableCollectionT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.ObjectModel.ObservableCollection";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.HashSetT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.HashSet";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.SortedSetT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.SortedSet";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.QueueT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.Queue";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.StackT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.Stack";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.LinkedListT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.LinkedList";
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, wellKnownTypes.ConcurrentBagT))
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Concurrent.ConcurrentBag";
        }

        if (!isCollection)
        {
            return (false, null);
        }

        var hasGenerateSerializerAttribute = HasGenerateSerializerAttribute(genericElementType as INamedTypeSymbol, wellKnownTypes);

        return (true, new CollectionTypeInfo
        (
            originalDefinition.ToDisplayString(s_typeNameFormat),
            genericElementType.ToDisplayString(s_typeNameFormat),
            genericElementType.IsUnmanagedType,
            genericElementType.SpecialType == SpecialType.System_String,
            hasGenerateSerializerAttribute,
            false,
            concreteTypeName
        ));
    }

    private static CollectionInfo? GetCollectionInfo(ISymbol member, WellKnownTypes wellKnownTypes)
    {
        var attribute = AttributeHelper.GetCollectionAttribute(member);
        
        var memberTypeSymbol = member is IPropertySymbol p ? p.Type : ((IFieldSymbol)member).Type;
        var (isCollection, _) = GetCollectionTypeInfo(memberTypeSymbol, wellKnownTypes);
        
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