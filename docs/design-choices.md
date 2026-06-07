# Design Choices

This document is the source of truth for the wire-contract decisions that FourSer.Gen currently enforces.

## Count-bearing collections

- `CountSizeReference` is a mirrored wire member, not a serialize-time source of truth.
- During serialization, the generator writes the actual count derived from the collection.
- During deserialization, the generator reads the count from the wire and assigns that value to the referenced member.
- Fixed-size collections remain required and non-null.

## Single-type polymorphic collections

- `PolymorphicMode.SingleTypeId` writes one discriminator for the whole collection.
- When `TypeIdProperty` is configured, serialization derives that discriminator from the actual collection contents.
- Stale `TypeIdProperty` values are ignored during serialization.
- During deserialization, the discriminator read from the wire is assigned back to the referenced `TypeIdProperty`.
- Empty or null `SingleTypeId` collections use the declared default option when one exists.

## Null canonicalization

- `string` keeps its existing canonicalization behavior: `null` writes as an empty string payload and reads back as `string.Empty`.
- Dynamic-count collections keep their existing canonicalization behavior: `null` writes as zero count and reads back as an empty collection instance.
- This canonicalization does not introduce nullable nested-object encoding.

## Nested generated references

- Nested generated reference members remain required on the wire.
- The generator does not emit nullable nested-object encodings for generated types.
- If a nested reference needs custom null handling, that must be expressed in a custom serializer that owns that wire format explicitly.

## IEnumerable replayability

- Pure `IEnumerable<T>` support is replayable-only across separate `GetPacketSize(obj)` and `Serialize(obj, ...)` calls.
- Each generated method enumerates such a sequence at most once internally.
- When a method needs both count/discriminator information and payload bytes, it may materialize the sequence once inside that method and reuse the prepared items there.
- The generator does not mutate user objects or cache enumerated results across separate generated method calls.

## Custom serializers

- `ISerializer<T>.Serialize(T, Stream)` and `ISerializer<T>.Deserialize(Stream)` are first-class contract members.
- Generated stream paths are emitted only when `SerializerGenerationMethods.Stream` is enabled.
- When emitted, generated stream paths call those stream methods directly.
- The generator must not bridge stream calls through the span-based methods on a custom serializer.

## Transport overload opt-ins

- `[GenerateSerializer]` emits only the span-based `ISerializable<T>` methods by default.
- `SerializerGenerationMethods.Stream` enables `Stream` serialization and deserialization methods.
- `SerializerGenerationMethods.BufferWriter` enables `IBufferWriter<byte>` serialization.
- `SerializerGenerationMethods.SequenceReader` enables `SequenceReader<byte>` deserialization.
- `SerializerGenerationMethods.PipeWriter` enables `PipeWriter` serialization.
- `SerializerGenerationMethods.PipeReader` enables `PipeReader` deserialization and uses the generated `SequenceReader<byte>` path internally.

## Referenced member ordering

- Referenced count and type-id members follow the declaration order enforced by the analyzers.
- The generator preserves that contract rather than reordering referenced members independently of source order.
