# Demo Tests

This directory hosts exploratory behavioural tests targeting the generated serializers.

- `Readers/` – TCD-specific reader harnesses that document binary layouts and feed the round-trip infrastructure.
- `Utils/` – Helpers and serializer stubs that support the demo scenarios (e.g., MFC string handling).
- `ReferenceFiles/` – Links to upstream client sources for cross-checking formats.
- `TcdResourceTheoryData.cs` – Enumerates `.tcd` fixtures from `%TEMP%\FourSer.Gen\TestFiles\Tcd.zip`, providing MemberData for parameterised tests. The archive is downloaded on demand if missing.
- `TcdRoundTripTests.cs` – Shared `[Theory]` suites:
  - `File_Has_Serializer` asserts every discovered `.tcd` has a registered serializer.
  - `RoundTripBinaryResources` runs deserialize/serialize/deserialize checks for mapped entries.

## Adding New Resources
1. Drop the new `.tcd` into the hosted ZIP (see repo testfiles branch) or keep it alongside the existing archive path inside `%TEMP%`.
2. Annotate the generated model type with `[TcdResource("FileName.tcd")]` so the registry can bind it.
3. Optionally extend `Readers/` with targeted assertions that validate business rules beyond round-tripping.

Running `dotnet test tests/FourSer.Tests.Behavioural/FourSer.Tests.Behavioural.csproj` will surface missing serializers and ensure the generic round trip passes for implemented ones.
