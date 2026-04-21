using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Core;

public static class CollectionUtilities
{
    private const string IReadOnlyCollectionPrefix = "System.Collections.Generic.IReadOnlyCollection<";
    private const string IReadOnlyListPrefix = "System.Collections.Generic.IReadOnlyList<";

    /// <summary>
    ///     Collection method mapping (consolidates 2 duplicate implementations)
    /// </summary>
    public static string GetCollectionAddMethod(ISymbol collectionType)
    {
        if (collectionType is not INamedTypeSymbol namedTypeSymbol)
        {
            return "Add";
        }

        if (namedTypeSymbol.IsGenericQueue()) return "Enqueue";
        if (namedTypeSymbol.IsGenericStack()) return "Push";
        if (namedTypeSymbol.IsGenericLinkedList()) return "AddLast";

        return "Add";
    }

    public static bool CanUseDirectByteCollectionPath(MemberToGenerate member)
    {
        return member.CollectionTypeInfo?.IsArray == true || member.IsList;
    }

    public static bool ShouldDeserializeIntoStagingCollection(MemberToGenerate member)
    {
        if (member.CollectionTypeInfo is not { } collectionTypeInfo)
        {
            return false;
        }

        if (collectionTypeInfo.RangeFactoryTypeName is not null)
        {
            return true;
        }

        return member.TypeName.StartsWith(IReadOnlyCollectionPrefix, StringComparison.Ordinal)
            || member.TypeName.StartsWith(IReadOnlyListPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Capacity constructor support (consolidates 2 duplicate implementations)
    /// </summary>
    public static bool SupportsCapacityConstructor(string collectionTypeName)
    {
        return collectionTypeName switch
        {
            "System.Collections.Generic.List" => true,
            "System.Collections.Generic.HashSet" => true,
            "System.Collections.Generic.SortedSet" => false,
            "System.Collections.Generic.Queue" => true,
            "System.Collections.Generic.Stack" => true,
            "System.Collections.ObjectModel.Collection" => false, // No capacity constructor
            "System.Collections.ObjectModel.ObservableCollection" => false, // No capacity constructor
            "System.Collections.Concurrent.ConcurrentBag" => false, // No capacity constructor
            "System.Collections.Generic.LinkedList" => false, // No capacity constructor
            _ => false
        };
    }

    /// <summary>
    ///     Collection instantiation logic for deserialization.
    /// </summary>
    public static string GenerateCollectionInstantiation(MemberToGenerate member, string countVar, string target)
    {
        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        if (member.CollectionTypeInfo?.IsArray == true)
        {
            return $"{target} = new {elementTypeName}[{countVar}];";
        }

        if (member.CollectionTypeInfo?.ConcreteTypeName != null)
        {
            var concreteTypeName = member.CollectionTypeInfo.Value.ConcreteTypeName;
            if (SupportsCapacityConstructor(concreteTypeName))
            {
                return $"{target} = new {concreteTypeName}<{elementTypeName}>({countVar});";
            }

            return $"{target} = new {concreteTypeName}<{elementTypeName}>();";
        }

        return $"{target} = new System.Collections.Generic.List<{elementTypeName}>({countVar});";
    }

    /// <summary>
    ///     Collection assignment logic for interface types after deserialization.
    /// </summary>
    public static string GenerateCollectionAssignment(MemberToGenerate member, string stagingVariableName, string finalTargetExpression)
    {
        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        if (member.CollectionTypeInfo?.RangeFactoryTypeName is { } rangeFactoryTypeName)
        {
            if (string.Equals(rangeFactoryTypeName, "System.Collections.Immutable.ImmutableStack", StringComparison.Ordinal))
            {
                return $"{finalTargetExpression} = {rangeFactoryTypeName}.CreateRange<{elementTypeName}>(global::System.Linq.Enumerable.Reverse({stagingVariableName}));";
            }

            return $"{finalTargetExpression} = {rangeFactoryTypeName}.CreateRange<{elementTypeName}>({stagingVariableName});";
        }

        if (member.CollectionTypeInfo?.IsArray == true)
        {
            return $"{finalTargetExpression} = {stagingVariableName}.ToArray();";
        }

        return $"{finalTargetExpression} = {stagingVariableName};";
    }
}
