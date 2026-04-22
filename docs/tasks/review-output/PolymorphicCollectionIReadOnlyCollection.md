# PolymorphicCollectionIReadOnlyCollection Review

## Verdict

Partially correct. The generated code should round-trip homogeneous `IReadOnlyCollection<IItem>` values on span and seekable-stream paths, but it does not fully match likely user expectations for this input because `IReadOnlyCollection<T>` is treated like a generic enumerable rather than a counted collection.

## Findings

- High: `Inventory.Serialize(Stream)` unnecessarily requires a seekable stream for `Items`, even though the source member is `IReadOnlyCollection<IItem>` and therefore already exposes `Count` (`tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyCollection/input.cs:12`). The generated method materializes `obj.Items`, reserves a placeholder count, and throws on `!stream.CanSeek` before backfilling that count (`tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyCollection/PolymorphicCollectionIReadOnlyCollection.RunGeneratorTest.verified.txt:274`, `:313`, `:318`, `:374`). The generator already knows `IReadOnlyCollection<T>` has `Count` (`src/FourSer.Gen/TypeInfoProvider.cs:290`) but routes non-indexable single-type polymorphic collections into the enumerable serializer path (`src/FourSer.Gen/CodeGenerators/Logic/CollectionSerializer.cs:244`, `:248`; `src/FourSer.Gen/CodeGenerators/Logic/PolymorphicSerializer.cs:423`, `:474`). That is a real behavior restriction, not just a code-shape issue.
- Medium: Both serialize overloads eagerly copy `obj.Items` into `itemsValidatedItems = Enumerable.ToList(obj.Items)` and then walk that list again for validation and again for writing (`tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyCollection/PolymorphicCollectionIReadOnlyCollection.RunGeneratorTest.verified.txt:160`, `:180`, `:209`, `:274`, `:294`, `:326`). For an `IReadOnlyCollection<T>`, that is redundant generated code: the count is already known, and a single enumerator pass after first-item discrimination would be enough.
- Low: Both deserialize overloads allocate `itemsStaging` and then immediately copy it into a second `List<IItem>` before assigning the property (`tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyCollection/PolymorphicCollectionIReadOnlyCollection.RunGeneratorTest.verified.txt:68`, `:102`, `:115`, `:149`). Since `Items` is typed as `IReadOnlyCollection<IItem>`, the staging list already satisfies the target contract.

## Performance opportunities

- Write the known collection count up front for `IReadOnlyCollection<T>` and serialize sequentially. That would remove the placeholder-count write, the seek/rewind pair, and the non-seekable stream limitation.
- Fuse discriminator selection, mixed-type validation, and item serialization into one pass over the source collection instead of `ToList` plus separate validation and write passes.
- Reuse the staging list directly during deserialization instead of copying it into another `List<IItem>`.

## Open questions / assumptions

- Assumed `IReadOnlyCollection<T>` should get the same count-aware stream treatment as other counted collections, even without random access.
- Assumed assigning a mutable `List<IItem>` to an `IReadOnlyCollection<IItem>` property is acceptable for this test case and not itself a defect.
- Assumed the serializer's choice to emit the first polymorphic option (`Sword` / `10`) for empty collections is intentional, since the generated serializer and deserializer are internally consistent on that point.
