namespace FourSer.Gen.CodeGenerators.Planning;

internal readonly record struct TargetCapabilities(
    bool HasStreamReadExactly,
    bool HasStreamWriteSpan,
    bool HasCollectionsMarshalAsSpan,
    bool HasCollectionsMarshalSetCount,
    bool HasDecimalGetBitsSpan,
    bool HasEnumerableTryGetNonEnumeratedCount);
