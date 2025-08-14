using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Core
{
    public static class GeneratorUtilities
    {
        /// <summary>
        /// Unified method name mapping (consolidates 4 duplicate implementations)
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
                "float" => "Single",
                "bool" => "Boolean",
                "double" => "Double",
                _ => TypeHelper.GetMethodFriendlyTypeName(typeName)
            };
        }

        /// <summary>
        /// Unified count expression generation (consolidates 4 duplicate implementations)
        /// </summary>
        public static string GetCountExpression(MemberToGenerate member, string memberName)
        {
            // Arrays use .Length property
            if (member.CollectionTypeInfo?.IsArray == true)
            {
                return $"obj.{memberName}.Length";
            }

            // IEnumerable and interface types that need Count() method
            if (member.CollectionTypeInfo?.CollectionTypeName?.Contains("IEnumerable") == true ||
                member.CollectionTypeInfo?.CollectionTypeName?.Contains("ICollection") == true ||
                member.CollectionTypeInfo?.CollectionTypeName?.Contains("IList") == true)
            {
                return $"obj.{memberName}.Count()";
            }

            // Most concrete collection types use .Count property
            // List<T>, HashSet<T>, Queue<T>, Stack<T>, ConcurrentBag<T>, LinkedList<T>, Collection<T>, etc.
            return $"obj.{memberName}.Count";
        }

        /// <summary>
        /// Unified polymorphic check (consolidates 4 duplicate implementations)
        /// </summary>
        public static bool ShouldUsePolymorphicSerialization(MemberToGenerate member)
        {
            // Only use polymorphic logic if explicitly configured
            if (member.CollectionInfo?.PolymorphicMode != PolymorphicMode.None)
                return true;

            // Or if SerializePolymorphic attribute is present with actual options
            if (member.PolymorphicInfo?.Options.IsEmpty == false)
                return true;

            return false;
        }

        /// <summary>
        /// Checks if the member is an unmanaged type.
        /// </summary>
        public static bool IsUnmanagedType(MemberToGenerate member) => member.IsUnmanagedType;

        /// <summary>
        /// Checks if the member is a string type.
        /// </summary>
        public static bool IsStringType(MemberToGenerate member) => member.IsStringType;

        /// <summary>
        /// Checks if the member has the GenerateSerializer attribute.
        /// </summary>
        public static bool HasGenerateSerializerAttribute(MemberToGenerate member) => member.HasGenerateSerializerAttribute;
    }
}
