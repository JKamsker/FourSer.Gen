using FourSer.Gen.Models;

namespace FourSer.Gen;

internal static class SerializerGenerationDefaults
{
    public static TypeToGenerate Apply(TypeToGenerate type, SerializerGenerationMethods additionalMethods)
    {
        if (additionalMethods == SerializerGenerationMethods.None)
        {
            return type;
        }

        var nestedTypes = type.NestedTypes
            .Select(nestedType => Apply(nestedType, additionalMethods))
            .ToEquatableArray();

        return type with
        {
            AdditionalMethods = type.AdditionalMethods | additionalMethods,
            NestedTypes = nestedTypes,
        };
    }
}
