using Microsoft.CodeAnalysis;

namespace FourSer.Gen.Helpers;

/// <summary>
///     Helper methods for working with serialization attributes
/// </summary>
public static class AttributeHelper
{
    public static AttributeData? GetCollectionAttribute(ISymbol member)
    {
        return member.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass is not null && a.AttributeClass.IsSerializeCollectionAttribute());
    }

    public static AttributeData? GetPolymorphicAttribute(ISymbol member)
    {
        return member.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass is not null && a.AttributeClass.IsSerializePolymorphicAttribute());
    }

    public static List<AttributeData> GetPolymorphicOptions(ISymbol member)
    {
        return member.GetAttributes()
            .Where(a => a.AttributeClass is not null && a.AttributeClass.IsPolymorphicOptionAttribute())
            .ToList();
    }

    public static (object Key, ITypeSymbol Type, bool IsDefault) GetPolymorphicOption(AttributeData optionAttribute)
    {
        var key = optionAttribute.ConstructorArguments[0].Value!;
        var type = (ITypeSymbol)optionAttribute.ConstructorArguments[1].Value!;
        var isDefault = optionAttribute.ConstructorArguments.Length > 2
            ? optionAttribute.ConstructorArguments[2].Value as bool? ?? false
            : optionAttribute.NamedArguments
                .FirstOrDefault(arg => arg.Key == "IsDefault")
                .Value.Value as bool? ?? false;

        return (key, type, isDefault);
    }

    public static bool HasGenerateSerializerAttribute(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(a => a.AttributeClass is not null && a.AttributeClass.IsGenerateSerializerAttribute());
    }

    public static FourSer.Gen.Models.SerializerGenerationMethods GetAdditionalMethods(AttributeData? generateSerializerAttribute)
    {
        if (generateSerializerAttribute is null)
        {
            return FourSer.Gen.Models.SerializerGenerationMethods.None;
        }

        if (generateSerializerAttribute.ConstructorArguments.Length > 0
            && generateSerializerAttribute.ConstructorArguments[0].Value is int constructorValue)
        {
            return (FourSer.Gen.Models.SerializerGenerationMethods)constructorValue;
        }

        var namedValue = generateSerializerAttribute.NamedArguments
            .FirstOrDefault(arg => arg.Key == "AdditionalMethods")
            .Value.Value;

        return namedValue is int value
            ? (FourSer.Gen.Models.SerializerGenerationMethods)value
            : FourSer.Gen.Models.SerializerGenerationMethods.None;
    }

    public static FourSer.Gen.Models.SerializerGenerationMethods GetAssemblyAdditionalMethods(IAssemblySymbol assemblySymbol)
    {
        var additionalMethods = FourSer.Gen.Models.SerializerGenerationMethods.None;
        foreach (var attribute in assemblySymbol.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            if (!attribute.AttributeClass.IsSerializerGenerationOptionsAttribute()
                && !attribute.AttributeClass.IsGenerateSerializerAttribute())
            {
                continue;
            }

            additionalMethods |= GetAdditionalMethods(attribute);
        }

        return additionalMethods;
    }

    public static string? GetCountSizeReference(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "CountSizeReference")
            .Value.Value?.ToString();
    }

    public static ITypeSymbol? GetCountType(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "CountType")
            .Value.Value as ITypeSymbol;
    }

    public static int? GetCountSize(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "CountSize")
            .Value.Value as int?;
    }

    public static string? GetTypeIdProperty(AttributeData? polymorphicAttribute)
    {
        return polymorphicAttribute?.ConstructorArguments.FirstOrDefault().Value?.ToString();
    }

    public static ITypeSymbol? GetTypeIdType(AttributeData? polymorphicAttribute)
    {
        return polymorphicAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdType")
            .Value.Value as ITypeSymbol;
    }

    public static int GetPolymorphicMode(AttributeData? collectionAttribute)
    {
        var polymorphicModeArg = collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "PolymorphicMode");

        // The enum value is returned as an int. 0=None, 1=SingleTypeId, 2=IndividualTypeIds
        return polymorphicModeArg?.Value.Value as int? ?? 0;
    }

    public static ITypeSymbol? GetCollectionTypeIdType(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdType")
            .Value.Value as ITypeSymbol;
    }

    public static string? GetCollectionTypeIdProperty(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdProperty")
            .Value.Value?.ToString();
    }

    public static bool GetUnlimited(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "Unlimited")
            .Value.Value as bool? ?? false;
    }
}
