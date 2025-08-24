using System.Collections.Immutable;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen;

/// <summary>
/// Provides services for extracting raw information about types to be serialized from Roslyn symbols.
/// This class is responsible for the first, fast layer of the source generation pipeline,
/// which performs a quick, shallow data extraction without expensive processing.
/// </summary>
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

    /// <summary>
    /// The entry point for the first layer of the source generator.
    /// It extracts the basic, unprocessed information for a given type.
    /// </summary>
    public static RawTypeToGenerate? GetRawSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        // We only handle top-level types. Nested types are handled recursively.
        if (typeSymbol.ContainingType != null)
        {
            return null;
        }

        return CreateRawTypeToGenerate(typeSymbol);
    }

    private static RawTypeToGenerate? CreateRawTypeToGenerate(INamedTypeSymbol typeSymbol)
    {
        if (!HasGenerateSerializerAttribute(typeSymbol))
        {
            return null;
        }

        var serializableMembers = GetSerializableMembers(typeSymbol);
        var nestedTypes = GetNestedTypes(typeSymbol);
        var hasSerializableBaseType = HasGenerateSerializerAttribute(typeSymbol.BaseType);

        return new RawTypeToGenerate
        (
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.IsValueType,
            typeSymbol.IsRecord,
            typeSymbol,
            serializableMembers,
            nestedTypes,
            hasSerializableBaseType
        );
    }

    private static bool HasBackingField(IPropertySymbol p)
    {
        // A simple heuristic to check for a backing field for auto-properties.
        foreach (var innerMember in p.ContainingType.GetMembers())
        {
            if (innerMember is IFieldSymbol { AssociatedSymbol: { } } f && SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, p))
            {
                return true;
            }
        }
        return false;
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

            // Read-write properties are serializable.
            if (p.SetMethod is not null)
            {
                return true;
            }

            // Read-only auto-properties with a backing field are also serializable.
            return HasBackingField(p);
        }

        if (member is IFieldSymbol field)
        {
            return field.DeclaredAccessibility == Accessibility.Public;
        }

        return false;
    }

    private static EquatableArray<RawMemberToGenerate> GetSerializableMembers(INamedTypeSymbol typeSymbol)
    {
        var membersWithLocation = new List<(RawMemberToGenerate, Location)>();
        var currentType = typeSymbol;

        // Walk up the inheritance chain to gather all members.
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var typeMembers = new List<(RawMemberToGenerate, Location)>();
            foreach (var member in currentType.GetMembers())
            {
                if (IsSerializableMember(member))
                {
                    typeMembers.Add(CreateRawMemberToGenerate(member));
                }
            }

            // Sort members by their declaration order in the source code.
            typeMembers.Sort((m1, m2) => m1.Item2.SourceSpan.Start.CompareTo(m2.Item2.SourceSpan.Start));
            membersWithLocation.InsertRange(0, typeMembers);
            currentType = currentType.BaseType;
        }

        return new(membersWithLocation.Select(m => m.Item1).ToImmutableArray());
    }

    private static (RawMemberToGenerate, Location) CreateRawMemberToGenerate(ISymbol member)
    {
        var memberTypeSymbol = member is IPropertySymbol p ? p.Type : ((IFieldSymbol)member).Type;

        var polymorphicAttribute = AttributeHelper.GetPolymorphicAttribute(member);
        var collectionAttribute = AttributeHelper.GetCollectionAttribute(member);
        var polymorphicOptions = AttributeHelper.GetPolymorphicOptions(member);

        var isReadOnly = false;
        var isInitOnly = false;
        if (member is IPropertySymbol prop)
        {
            isReadOnly = prop.SetMethod is null;
            isInitOnly = prop.SetMethod?.IsInitOnly ?? false;
        }
        else if (member is IFieldSymbol field)
        {
            isReadOnly = field.IsReadOnly;
        }

        var location = member.Locations.First();
        var rawMember = new RawMemberToGenerate
        (
            member.Name,
            memberTypeSymbol.ToDisplayString(s_typeNameFormat),
            memberTypeSymbol,
            collectionAttribute,
            polymorphicAttribute,
            polymorphicOptions.ToImmutableArray(),
            isReadOnly,
            isInitOnly,
            location
        );

        return (rawMember, location);
    }

    private static EquatableArray<RawTypeToGenerate> GetNestedTypes(INamedTypeSymbol parentType)
    {
        var nestedTypes = ImmutableArray.CreateBuilder<RawTypeToGenerate>();
        foreach (var member in parentType.GetMembers())
        {
            if (member is INamedTypeSymbol nestedTypeSymbol)
            {
                var nestedType = CreateRawTypeToGenerate(nestedTypeSymbol);
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

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute")
            {
                return true;
            }
        }

        return false;
    }
}