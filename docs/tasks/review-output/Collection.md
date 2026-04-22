# Collection Review

## Verdict
Functionally correct for the current FourSer collection contract and already optimized on the hot path. The only material expectation mismatch is that the source member is non-nullable, but the generated serializer still treats it as optional and canonicalizes `null` to an empty list.

## Findings
1. Low: The generated serializer ignores the source nullability contract for `Numbers`. `tests/FourSer.Tests/GeneratorTestCases/Collection/input.cs:7` declares `List<int> Numbers` in a project with nullable enabled, but `tests/FourSer.Tests/GeneratorTestCases/Collection/Collection.RunGeneratorTest.verified.txt:29`, `:99-105`, and `:132-138` accept `null` and encode it as count `0`, while `:46-47` and `:74-75` always materialize a non-null `List<int>` on deserialize. This matches the current runtime contract for dynamic-count collections in `tests/FourSer.Tests.Behavioural/Collections/CollectionTests.cs:96-108` and `tests/FourSer.Tests/OptimizationCollectionFailureTests.cs:100-153`, and it also matches the generator's current `List<T>` metadata in `src/FourSer.Gen/TypeInfoProvider.cs:241-250`, so it is not a functional regression. It is still likely surprising for a user who reads the member as non-nullable.

## Performance opportunities
- No major batching gap on the hot path: the little-endian span/stream paths already emit one count prefix plus one bulk payload transfer in `tests/FourSer.Tests/GeneratorTestCases/Collection/Collection.RunGeneratorTest.verified.txt:105-109`, `:138-142`, `:45-51`, and `:73-79`.
- The big-endian fallback repeatedly recomputes `CollectionsMarshal.AsSpan(...)` inside loop headers and bodies in `tests/FourSer.Tests/GeneratorTestCases/Collection/Collection.RunGeneratorTest.verified.txt:55-57`, `:83-85`, `:113-115`, and `:146-148`. Hoisting the span once would remove redundant calls.
- Both deserializers keep redundant temporaries (`numbers`, `numbersValue`, `obj`) before returning in `tests/FourSer.Tests/GeneratorTestCases/Collection/Collection.RunGeneratorTest.verified.txt:44-62` and `:72-90`. This is minor, but it is duplicate generated code/noise.

## Open questions / assumptions
- Assuming current intended behavior is "dynamic-count collections serialize `null` as zero-count and deserialize zero-count as empty," this case is correct.
- If collection nullability annotations are intended to affect runtime behavior, this case should probably not use the nullable/empty canonicalization path for `List<int> Numbers`.
