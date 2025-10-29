# Demo Tests

This directory hosts exploratory behavioural tests targeting the generated serializers.

- `Readers/` – TCD-specific reader harnesses that document binary layouts and feed the round-trip infrastructure.
- `Utils/` – Helpers and serializer stubs that support the demo scenarios (e.g., MFC string handling).
- `ReferenceFiles/` – Links to upstream client sources for cross-checking formats.
- `TcdResourceTheoryData.cs` – Enumerates `.tcd` fixtures from `%TEMP%\FourSer.Gen\TestFiles\Tcd.zip`, providing MemberData for parameterised tests. The archive is downloaded on demand if missing.
- `TcdRoundTripTests.cs` – Shared `[Theory]` suites:
  - `File_Has_Serializer` asserts every discovered `.tcd` has a registered serializer.
  - `RoundTripBinaryResources` runs deserialize/serialize/deserialize checks for mapped entries.

## Tutorial: Implementing a Missing TCD Reader

Follow these steps whenever the test suite reports an unmapped `.tcd` resource.

### 1. Discover which files still need serializers
Run the behavioural tests and inspect the failures:

```powershell
dotnet test tests/FourSer.Tests.Behavioural/FourSer.Tests.Behavioural.csproj --filter TcdRoundTripTests
```

`File_Has_Serializer` failures list the resource names (e.g. `TNPC00030000.tcd`). Once a serializer exists the failure will move to the round-trip phase if the model does not yet match the binary layout.

### 2. Inspect the raw payload (optional sanity check)
All fixtures live in `%TEMP%\FourSer.Gen\TestFiles\Tcd.zip`. You can peek at headers directly from PowerShell:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = "$env:TEMP\FourSer.Gen\TestFiles\Tcd.zip"
[IO.Compression.ZipFile]::OpenRead($zip).GetEntry('Tcd/TGate.tcd').Open().ReadByte()
```

A quick byte dump helps to confirm whether a file is structured binary (begins with a count, etc.) or plain text (e.g. `TCashShop.tcd`).

### 3. Locate the native layout definition
The `ReferenceFiles` link in this folder points at the original client sources. Typical binary charts are loaded via functions inside `TClient/TChart/TChart.cpp` named `InitT...`. Use `rg` or your editor search to navigate to the matching initializer, for example:

```powershell
rg "InitTGATE" tests/FourSer.Tests.Behavioural/Demo/ReferenceFiles/TClient/TClient/TChart.cpp
```

The fields read by `CArchive::operator>>` map one-to-one to the properties you need in C#. Arrays or repeated `for` loops indicate fixed-size buffers or count-prefixed collections.

Guidelines for mapping common patterns:

- `WORD wCount; ar >> wCount; for (...)` → decorate a `List<T>` with `[SerializeCollection(CountType = typeof(ushort))]`.
- Fixed iteration loops (`for (int i = 0; i < 5; ++i)`) → use `[SerializeCollection(CountSize = 5)]` and an array.
- `CString` values (written with `>> pRecord->m_strName`) use the MFC string format; apply `[Serializer(typeof(MfcAnsiStringSerializer))]` when the native loader treats the data as ANSI (no BOM) or `MfcStringSerializer` for UTF‑16+MFC count strings.
- Plain text files being read with `CStdioFile::ReadString` (e.g. `TCashShop.tcd`) should use `PlainAsciiStringSerializer`.

### 4. Create the reader model
Add a new file under `Readers/` named after the resource, e.g. `TGateReader.cs`. Keep one `.cs` file per `.tcd`. A minimal template looks like this:

```csharp
using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TGate.tcd")]
    [GenerateSerializer]
    public partial class TGateCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TGateEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TGateEntry
    {
        // map fields exactly in the order read by the C++ loader
    }
}
```

Keep helper types (e.g. an `Entry` class or nested `Slot` type) inside the same file so it remains specific to the owning resource.
If you need a refresher on the attribute- and generator-based serialization framework used here, consult the repository root `README.md`, which documents the available attributes, serializer contracts, and code-generation pipeline in greater detail.

### 5. Validate with the test suite
Re-run the behavioural tests. When `File_Has_Serializer` no longer reports the resource and `RoundTripBinaryResources` passes (or at least moves on to the next file), the serializer matches the binary layout.

```powershell
dotnet test tests/FourSer.Tests.Behavioural/FourSer.Tests.Behavioural.csproj --filter TcdRoundTripTests
```

Iterate through the remaining failures until the suite is green.

## Adding New Resources
1. Drop the new `.tcd` into the hosted ZIP (see repo testfiles branch) or keep it alongside the existing archive path inside `%TEMP%`.
2. Create a dedicated file under `Readers/`, follow the tutorial above to model the binary layout, and annotate the root type with `[TcdResource("FileName.tcd")]` so the registry can bind it.
3. Optionally extend `Readers/` with targeted assertions that validate business rules beyond round-tripping.
4. Run the behavioural test suite to confirm coverage.

Running `dotnet test tests/FourSer.Tests.Behavioural/FourSer.Tests.Behavioural.csproj` will surface missing serializers and ensure the generic round trip passes for implemented ones.
