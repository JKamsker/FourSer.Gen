namespace Serializer.Generator.Helpers;

/// <summary>
/// Helper methods for type name conversions in code generation
/// </summary>
public static class TypeHelper
{
    /// <summary>
    /// Converts a type name to the appropriate Read method name for Span extensions
    /// </summary>
    public static string GetReadMethodName(string typeName)
    {
        return typeName switch
        {
            "byte" => "ReadByte",
            "sbyte" => "ReadSByte",
            "short" => "ReadInt16",
            "ushort" => "ReadUInt16", 
            "int" => "ReadInt32",
            "uint" => "ReadUInt32",
            "long" => "ReadInt64",
            "ulong" => "ReadUInt64",
            "float" => "ReadSingle",
            "double" => "ReadDouble",
            "bool" => "ReadBoolean",
            _ => $"Read{GetMethodFriendlyTypeName(typeName)}"
        };
    }

    /// <summary>
    /// Converts a type name to the appropriate Write method name for Span extensions  
    /// </summary>
    public static string GetWriteMethodName(string typeName)
    {
        return typeName switch
        {
            "byte" => "WriteByte",
            "sbyte" => "WriteSByte", 
            "short" => "WriteInt16",
            "ushort" => "WriteUInt16",
            "int" => "WriteInt32",
            "uint" => "WriteUInt32",
            "long" => "WriteInt64",
            "ulong" => "WriteUInt64",
            "float" => "WriteSingle",
            "double" => "WriteDouble", 
            "bool" => "WriteBoolean",
            _ => $"Write{GetMethodFriendlyTypeName(typeName)}"
        };
    }

    /// <summary>
    /// Gets the sizeof expression for a type
    /// </summary>
    public static string GetSizeOfExpression(string typeName)
    {
        return $"sizeof({typeName})";
    }

    /// <summary>
    /// Gets the default count type for collections (int)
    /// </summary>
    public static string GetDefaultCountType() => "int";

    /// <summary>
    /// Converts a type name to a method-friendly version (e.g., "int" -> "Int32")
    /// </summary>
    public static string GetMethodFriendlyTypeName(string typeName)
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
            // Unsupported types - map to supported alternatives
            "char" => "UInt16", // char is 2 bytes, same as ushort
            "decimal" => "Int64", // decimal can be represented as long for simple cases
            _ => GetSimpleTypeName(typeName)
        };
    }

    /// <summary>
    /// Gets the simple type name from a fully qualified name
    /// </summary>
    public static string GetSimpleTypeName(string? fullyQualifiedName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedName)) return string.Empty;
        var lastDot = fullyQualifiedName!.LastIndexOf('.');
        if (lastDot == -1)
        {
            return fullyQualifiedName;
        }
        return fullyQualifiedName.Substring(lastDot + 1);
    }

    /// <summary>
    /// Determines if a collection contains byte elements and can use bulk operations
    /// </summary>
    public static bool IsByteCollection(string? elementTypeName)
    {
        return elementTypeName == "byte";
    }
}
