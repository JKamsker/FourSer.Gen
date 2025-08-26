using System.Collections.Immutable;
using System.Diagnostics;
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
        var defaultSerializers = GetDefaultSerializers(typeSymbol, typeSymbol.ContainingAssembly);

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new TypeToGenerate
        (
            typeSymbol.Name,
            ns,
            typeSymbol.IsValueType,
            typeSymbol.IsRecord,
            serializableMembers,
            nestedTypes,
            hasSerializableBaseType,
            constructorInfo,
            new(defaultSerializers)
        );
    }

    private static ImmutableArray<DefaultSerializerInfo> GetDefaultSerializers(INamedTypeSymbol typeSymbol, IAssemblySymbol assemblySymbol)
    {
        var defaultSerializers = new Dictionary<string, DefaultSerializerInfo>();

        // Get assembly-level default serializers
        var assemblyAttributes = assemblySymbol.GetAttributes();
        foreach (var attribute in assemblyAttributes)
        {
            if (attribute.AttributeClass is not null && attribute.AttributeClass.IsDefaultSerializerAttribute())
            {
                var targetType = attribute.ConstructorArguments[0].Value as ITypeSymbol;
                var serializerType = attribute.ConstructorArguments[1].Value as ITypeSymbol;

                if (targetType != null && serializerType != null)
                {
                    var targetTypeName = targetType.ToDisplayString(s_typeNameFormat);
                    defaultSerializers[targetTypeName] = new DefaultSerializerInfo(targetTypeName, serializerType.ToDisplayString(s_typeNameFormat));
                }
            }
        }

        // Get class-level default serializers, potentially overriding assembly-level ones
        var attributes = typeSymbol.GetAttributes();
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not null && attribute.AttributeClass.IsDefaultSerializerAttribute())
            {
                var targetType = attribute.ConstructorArguments[0].Value as ITypeSymbol;
                var serializerType = attribute.ConstructorArguments[1].Value as ITypeSymbol;

                if (targetType != null && serializerType != null)
                {
                    var targetTypeName = targetType.ToDisplayString(s_typeNameFormat);
                    defaultSerializers[targetTypeName] = new DefaultSerializerInfo(targetTypeName, serializerType.ToDisplayString(s_typeNameFormat));
                }
            }
        }

        return defaultSerializers.Values.ToImmutableArray();
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
                var bestConstructor = FindBestConstructor(publicConstructors, members, typeSymbol);
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

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors, EquatableArray<MemberToGenerate> members, INamedTypeSymbol typeSymbol)
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

            if (AllParametersMatch(c, members, typeSymbol))
            {
                return c;
            }
        }

        return null;
    }

    private static bool AllParametersMatch(IMethodSymbol constructor, EquatableArray<MemberToGenerate> members, INamedTypeSymbol typeSymbol)
    {
        foreach (var p in constructor.Parameters)
        {
            var parameterMatches = false;
            foreach (var m in members)
            {
                if (string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var memberSymbol = FindMember(typeSymbol, m.Name);
                    if (memberSymbol != null)
                    {
                        var memberTypeSymbol = memberSymbol switch
                        {
                            IPropertySymbol prop => prop.Type,
                            IFieldSymbol field => field.Type,
                            _ => null
                        };

                        if (memberTypeSymbol != null && SymbolEqualityComparer.Default.Equals(p.Type, memberTypeSymbol))
                        {
                            parameterMatches = true;
                            break;
                        }
                    }
                }
            }

            if (!parameterMatches)
            {
                return false;
            }
        }

        return true;
    }

    private static ISymbol? FindMember(INamedTypeSymbol typeSymbol, string memberName)
    {
        var currentType = typeSymbol;
        while (currentType != null)
        {
            var member = currentType.GetMembers(memberName).FirstOrDefault();
            if (member != null)
            {
                return member;
            }
            currentType = currentType.BaseType;
        }
        return null;
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

        var resolvedMembers = ResolveMemberReferences(members);
        return new(resolvedMembers.ToImmutableArray());
    }

    private static List<MemberToGenerate> ResolveMemberReferences(List<MemberToGenerate> members)
    {
        var memberMap = new Dictionary<string, int>();
        for (var i = 0; i < members.Count; i++)
        {
            memberMap[members[i].Name] = i;
        }

        var newMembers = members.ToList();
        for (var i = 0; i < newMembers.Count; i++)
        {
            var member = newMembers[i];
            var newCollectionInfo = member.CollectionInfo;
            if (member.CollectionInfo is { } collectionInfo)
            {
                int? countRefIndex = null;
                if (collectionInfo.CountSizeReference is { } countRef)
                {
                    if (memberMap.TryGetValue(countRef, out var index))
                    {
                        countRefIndex = index;
                        var oldMember = newMembers[index];
                        newMembers[index] = oldMember with { IsCountSizeReferenceFor = i };
                    }
                }

                var countTypeSize = collectionInfo.CountType is null
                    ? (int?)null
                    : TypeHelper.GetSizeOf(collectionInfo.CountType);

                newCollectionInfo = collectionInfo with
                {
                    CountSizeReferenceIndex = countRefIndex,
                    CountTypeSizeInBytes = countTypeSize,
                };
            }

            var newPolymorphicInfo = member.PolymorphicInfo;
            if (member.PolymorphicInfo is { } polyInfo)
            {
                int? typeIdRefIndex = null;
                if (polyInfo.TypeIdProperty is { } typeIdRef)
                {
                    if (memberMap.TryGetValue(typeIdRef, out var index))
                    {
                        typeIdRefIndex = index;
                        var oldMember = newMembers[index];
                        newMembers[index] = oldMember with { IsTypeIdPropertyFor = i };
                    }
                }

                var typeIdSize = TypeHelper.GetSizeOf(polyInfo.TypeIdType);

                newPolymorphicInfo = polyInfo with
                {
                    TypeIdPropertyIndex = typeIdRefIndex,
                    TypeIdSizeInBytes = typeIdSize
                };
            }

            newMembers[i] = member with
            {
                CollectionInfo = newCollectionInfo,
                PolymorphicInfo = newPolymorphicInfo
            };
        }

        return newMembers;
    }

    private static (MemberToGenerate, Location) CreateMemberToGenerate(ISymbol m)
    {
        var memberTypeSymbol = m is IPropertySymbol p ? p.Type : ((IFieldSymbol)m).Type;
        var (isCollection, collectionTypeInfo) = GetCollectionTypeInfo(memberTypeSymbol);
        var isList = memberTypeSymbol.OriginalDefinition is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericList();
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
        var collectionInfo = GetCollectionInfo(m);


        var memberToGenerate = new MemberToGenerate
        (
            Name: m.Name,
            TypeName: memberTypeSymbol.ToDisplayString(s_typeNameFormat),
            IsValueType: memberTypeSymbol.IsValueType,
            IsUnmanagedType: memberTypeSymbol.IsUnmanagedType,
            IsStringType: memberTypeSymbol.SpecialType == SpecialType.System_String,
            HasGenerateSerializerAttribute: memberHasGenerateSerializerAttribute,
            IsList: isList,
            ListTypeArgument: listTypeArgumentInfo,
            CollectionInfo: collectionInfo,
            PolymorphicInfo: polymorphicInfo,
            IsCollection: isCollection,
            CollectionTypeInfo: collectionTypeInfo,
            IsReadOnly: isReadOnly,
            IsInitOnly: isInitOnly,
            IsCountSizeReferenceFor: null,
            IsTypeIdPropertyFor: null,
            CustomSerializer: GetCustomSerializer(m)
        );

        return (memberToGenerate, location);
    }

    private static CustomSerializerInfo? GetCustomSerializer(ISymbol member)
    {
        var attribute = member.GetAttributes().FirstOrDefault(ad => ad.AttributeClass is not null && ad.AttributeClass.IsSerializerAttribute());

        if (attribute == null)
        {
            return null;
        }

        var serializerType = attribute.ConstructorArguments[0].Value as ITypeSymbol;
        return serializerType != null ? new CustomSerializerInfo(serializerType.ToDisplayString(s_typeNameFormat)) : null;
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

        if (typeSymbol.GetAttributes().Any(ad => ad.AttributeClass is not null && ad.AttributeClass.IsGenerateSerializerAttribute()))
        {
            return true;
        }
        
        // Also good: TypeOfClass implements ISerializable<TypeOfClass>
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var isISerializable = iface.OriginalDefinition.IsISerializable();
            
            if (isISerializable)
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
        var defaultSerializers = GetDefaultSerializers(nestedTypeSymbol, nestedTypeSymbol.ContainingAssembly);

        var constructorInfo = GetConstructorInfo(nestedTypeSymbol, nestedMembers);

        var ns = nestedTypeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : nestedTypeSymbol.ContainingNamespace.ToDisplayString();

        return new TypeToGenerate
        (
            nestedTypeSymbol.Name,
            ns,
            nestedTypeSymbol.IsValueType,
            nestedTypeSymbol.IsRecord,
            nestedMembers,
            deeperNestedTypes,
            hasSerializableBaseType,
            constructorInfo,
            new(defaultSerializers)
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
                typeSymbol,
                elementType.ToDisplayString(s_typeNameFormat),
                elementType.IsUnmanagedType,
                elementType.SpecialType == SpecialType.System_String,
                arrayElementHasGenerateSerializerAttribute,
                true,
                null,
                false
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
        var isCollection = false;
        var isPureEnumerable = false;

        if (originalDefinition is not INamedTypeSymbol originalNamedTypeSymbol)
        {
            return (false, null);
        }

        if (originalNamedTypeSymbol.IsGenericList())
        {
            isCollection = true;
            concreteTypeName = null;
        }
        else if (originalNamedTypeSymbol.IsGenericIList() || originalNamedTypeSymbol.IsGenericICollection())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.List";
        }
        else if (originalNamedTypeSymbol.IsGenericIEnumerable())
        {
            isCollection = true;
            isPureEnumerable = true;
            concreteTypeName = "System.Collections.Generic.List";
        }
        else if (originalNamedTypeSymbol.IsObjectModelCollection())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.ObjectModel.Collection";
        }
        else if (originalNamedTypeSymbol.IsObjectModelObservableCollection())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.ObjectModel.ObservableCollection";
        }
        else if (originalNamedTypeSymbol.IsGenericHashSet())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.HashSet";
        }
        else if (originalNamedTypeSymbol.IsGenericSortedSet())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.SortedSet";
        }
        else if (originalNamedTypeSymbol.IsGenericQueue())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.Queue";
        }
        else if (originalNamedTypeSymbol.IsGenericStack())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.Stack";
        }
        else if (originalNamedTypeSymbol.IsGenericLinkedList())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Generic.LinkedList";
        }
        else if (originalNamedTypeSymbol.IsConcurrentConcurrentBag())
        {
            isCollection = true;
            concreteTypeName = "System.Collections.Concurrent.ConcurrentBag";
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
            concreteTypeName,
            isPureEnumerable
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
            null,
            null,
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

        var typeIdProperty = AttributeHelper.GetTypeIdProperty(attribute)
            ?? AttributeHelper.GetCollectionTypeIdProperty(collectionAttribute);

        var typeIdType = AttributeHelper.GetTypeIdType(attribute)
            ?? AttributeHelper.GetCollectionTypeIdType(collectionAttribute);

        var polymorphicOptions = GetPolymorphicOptions(options);

        var enumUnderlyingType = typeIdType is { TypeKind: TypeKind.Enum }
            ? ((INamedTypeSymbol)typeIdType).EnumUnderlyingType!.ToDisplayString()
            : null;

        var typeIdTypeString = GetTypeIdType(member, typeIdType, typeIdProperty, polymorphicOptions);

        return new PolymorphicInfo
        (
            TypeIdProperty: typeIdProperty,
            TypeIdType: typeIdTypeString,
            Options: new(polymorphicOptions),
            EnumUnderlyingType: enumUnderlyingType,
            TypeIdPropertyIndex: null,
            TypeIdSizeInBytes: null
        );
    }

    private static string GetTypeIdType
    (
        ISymbol member,
        ITypeSymbol? typeIdType,
        string? typeIdProperty,
        ImmutableArray<PolymorphicOption> polymorphicOptions
    )
    {
        // Determine type ID type string in order of precedence:
        // 1. Take whatever is specified explicitly as typeIdType
        // 2. If previous null: take the type of the property "typeIdProperty"
        // 3. If previous null: polymorphicOptions.FirstOrDefault().Key.GetType().Name 
        // 4. If previous null: int
        var typeIdTypeString = typeIdType?.ToDisplayString();
        if (!string.IsNullOrEmpty(typeIdTypeString))
        {
            return typeIdTypeString!;
        }

        // Try to get the type from the typeIdProperty
        var containingType = member.ContainingType;
        var referencedSymbol = containingType.GetMembers(typeIdProperty!).FirstOrDefault();

        typeIdTypeString = referencedSymbol switch
        {
            IPropertySymbol propertySymbol => propertySymbol.Type.ToDisplayString(s_typeNameFormat),
            IFieldSymbol fieldSymbol => fieldSymbol.Type.ToDisplayString(s_typeNameFormat),
            _ => typeIdTypeString
        };
        
        if(!string.IsNullOrEmpty(typeIdTypeString))
        {
            return typeIdTypeString!;
        }

        typeIdTypeString = polymorphicOptions.FirstOrDefault().Key.GetType().Name;
        if(!string.IsNullOrEmpty(typeIdTypeString))
        {
            return typeIdTypeString!;
        }
        
        return "int";
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
