using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Core;

public static class CollectionUtilities
{
    /// <summary>
    ///     Collection method mapping (consolidates 2 duplicate implementations)
    /// </summary>
    public static string GetCollectionAddMethod(string collectionTypeName)
    {
        return collectionTypeName switch
        {
            "System.Collections.Generic.Queue<T>" => "Enqueue",
            "System.Collections.Generic.Stack<T>" => "Push",
            "System.Collections.Generic.LinkedList<T>" => "AddLast",
            _ => "Add"
        };
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
    public static string GenerateCollectionAssignment(MemberToGenerate member, string tempVarName)
    {
        if (member.CollectionTypeInfo?.ConcreteTypeName != null)
        {
            return $"obj.{member.Name} = {tempVarName};";
        }

        return string.Empty;
    }
}