using Microsoft.CodeAnalysis;

namespace FourSer.Analyzers.Helpers
{
    public static class TypeSymbolExtensions
    {
        public static bool IsIntegralType(this ITypeSymbol type)
        {
            return type.SpecialType >= SpecialType.System_SByte && type.SpecialType <= SpecialType.System_UInt64;
        }
    }
}
