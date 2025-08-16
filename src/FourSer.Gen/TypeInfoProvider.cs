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

        var hasSerializableBaseType = typeSymbol.BaseType?.GetAttributes()
            .Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute") ?? false;

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
        var constructors = typeSymbol.Constructors
            .Where(c => !c.IsImplicitlyDeclared)
            .ToList();

        bool hasParameterlessCtor = constructors.Any(c => c.Parameters.Length == 0);
        bool shouldGenerate = members.Any(m => m.IsReadOnly);

        if (!shouldGenerate)
        {
            var publicConstructors = constructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .ToList();

            if (publicConstructors.Any())
            {
                var bestConstructor = publicConstructors
                    .FirstOrDefault(c => c.Parameters.Length == members.Count() && c.Parameters.All(p =>
                        members.Any(m => string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase) &&
                                         m.TypeName == p.Type.ToDisplayString(s_typeNameFormat))));

                if (bestConstructor is not null)
                {
                    var parameters = bestConstructor.Parameters
                        .Select(p => new ParameterInfo(p.Name, p.Type.ToDisplayString(s_typeNameFormat)))
                        .ToImmutableArray();
                    return new ConstructorInfo(new EquatableArray<ParameterInfo>(parameters), false, hasParameterlessCtor);
                }
            }
        }

        var generatedParameters = members
            .Select(m => new ParameterInfo(m.Name, m.TypeName))
            .ToImmutableArray();

        return new ConstructorInfo(new EquatableArray<ParameterInfo>(generatedParameters), true, hasParameterlessCtor);
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
            return p.ContainingType.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f => SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, p));
        }

        if (member is IFieldSymbol f)
        {
            return f.DeclaredAccessibility == Accessibility.Public;
        }

        return false;
    }

    private static EquatableArray<MemberToGenerate> GetSerializableMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<MemberToGenerate>();
        var currentType = typeSymbol;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var typeMembers = currentType.GetMembers()
                .Where(IsSerializableMember)
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

                        bool isReadOnly = false;
                        if (m is IPropertySymbol prop)
                        {
                            isReadOnly = prop.SetMethod is null;
                        }
                        else if (m is IFieldSymbol field)
                        {
                            isReadOnly = field.IsReadOnly;
                        }

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
                            isReadOnly
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
                    .Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute"))
            {
                var nestedMembers = GetSerializableMembers(nestedTypeSymbol);
                var deeperNestedTypes = GetNestedTypes(nestedTypeSymbol);

                var hasSerializableBaseType = nestedTypeSymbol.BaseType?.GetAttributes()
                    .Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute") ?? false;

                var constructorInfo = GetConstructorInfo(nestedTypeSymbol, nestedMembers);

                nestedTypes.Add
                (
                    new TypeToGenerate
                    (
                        nestedTypeSymbol.Name,
                        nestedTypeSymbol.ContainingNamespace.ToDisplayString(),
                        nestedTypeSymbol.IsValueType,
                        nestedMembers,
                        deeperNestedTypes,
                        hasSerializableBaseType,
                        constructorInfo
                    )
                );
            }
        }

        return new EquatableArray<TypeToGenerate>(nestedTypes.ToImmutable());
    }

    private static (bool IsCollection, CollectionTypeInfo? CollectionTypeInfo) GetCollectionTypeInfo(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            var elementType = arrayTypeSymbol.ElementType;
            return (true, new CollectionTypeInfo
            (
                typeSymbol.ToDisplayString(s_typeNameFormat),
                elementType.ToDisplayString(s_typeNameFormat),
                elementType.IsUnmanagedType,
                elementType.SpecialType == SpecialType.System_String,
                elementType.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute"),
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

        return (true, new CollectionTypeInfo
        (
            originalDefinition,
            genericElementType.ToDisplayString(s_typeNameFormat),
            genericElementType.IsUnmanagedType,
            genericElementType.SpecialType == SpecialType.System_String,
            genericElementType.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute"),
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

        var hasPolymorphicOptions = options.Any();
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