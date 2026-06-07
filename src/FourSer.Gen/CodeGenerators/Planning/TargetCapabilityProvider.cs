namespace FourSer.Gen.CodeGenerators.Planning;

internal static class TargetCapabilityProvider
{
    public static TargetCapabilities GetCapabilities()
    {
        return new TargetCapabilities(
            HasStreamReadExactly: true,
            HasStreamWriteSpan: true,
            HasCollectionsMarshalAsSpan: true,
            HasCollectionsMarshalSetCount: true,
            HasDecimalGetBitsSpan: true,
            HasEnumerableTryGetNonEnumeratedCount: true);
    }
}
