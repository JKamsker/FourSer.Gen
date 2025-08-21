# FourSer.Gen - Binary Serialization Source Generator

FourSer.Gen is a .NET 9 source generator that automatically creates high-performance binary serialization and deserialization code for classes and structs using attributes and conventions.

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Working Effectively

### Bootstrap and Build the Repository

**CRITICAL: Install .NET 9 SDK first** - The system may only have .NET 8 by default:
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --channel 9.0
export PATH="/home/runner/.dotnet:$PATH"
```

**Bootstrap, build, and test the repository:**
```bash
export PATH="/home/runner/.dotnet:$PATH"  # Always ensure .NET 9 is in PATH
dotnet restore                            # ~15 seconds
dotnet build --no-restore -c Release     # ~10 seconds  
dotnet test --no-build -c Release --verbosity normal  # ~8 seconds
```

**CRITICAL BUILD TIMINGS - NEVER CANCEL:**
- `dotnet restore`: **15 seconds** (clean) / **1 second** (incremental) - Set timeout to 300+ seconds
- `dotnet build`: **10 seconds** (clean) / **2 seconds** (incremental) - Set timeout to 300+ seconds  
- `dotnet test`: **8 seconds** - Set timeout to 300+ seconds
- ALL operations combined: **35 seconds maximum** (clean build) - Set timeout to 600+ seconds

**Package validation (after successful build):**
```bash
dotnet pack src/FourSer.Gen.Nuget/FourSer.Gen.Nuget.csproj --no-build -c Release -o out  # <1 second
dotnet build tests/Serializer.Package.Tests/Serializer.Package.Tests.sln -c Release      # ~2 seconds
dotnet run --project tests/Serializer.Package.Tests/Serializer.Package.Tests.csproj --no-build -c Release  # <1 second
```
This validates the NuGet package works correctly by creating a package and testing it in isolation.

### Run Integration Tests

**Run the Consumer integration tests:**
```bash
dotnet run --project src/FourSer.Consumer --no-build -c Release  # <1 second
```
This tests the source generator functionality with real-world serialization scenarios. Most tests pass - 2 known failures are acceptable.

## Validation Requirements

**ALWAYS run through complete validation scenarios after making changes:**

### Manual Validation Scenarios
1. **Basic Serialization Test**: Add a simple `[GenerateSerializer]` class to `src/FourSer.Consumer/UseCases/`, rebuild with `dotnet build -c Release`, and run consumer with `dotnet run --project src/FourSer.Consumer -c Release` to verify serialization/deserialization works end-to-end.
2. **Source Generator Test**: Add a new `[GenerateSerializer]` class in Consumer project, build, and verify generated code compiles without errors in the build output.
3. **Collection Serialization Test**: Test different `[SerializeCollection]` configurations (like `CountType = typeof(byte)`) by running consumer tests and checking serialization output.
4. **Format Validation**: Always run `dotnet format --verify-no-changes --no-restore` to ensure code meets formatting standards.

### Code Quality Validation
```bash
dotnet format --no-restore     # Fix formatting issues - ALWAYS run before committing
dotnet format --verify-no-changes --no-restore  # Verify formatting is correct
```

**ALWAYS run these validation steps before you are done or the CI (.github/workflows/ci.yml) will fail.**

## Project Structure and Navigation

### Key Projects
```
src/
├── FourSer.Contracts/          # Attributes and interfaces (start here for API changes)
│   ├── ISerializable.cs           # Main serialization interface
│   ├── GenerateSerializerAttribute.cs  # Core attribute for classes/structs
│   ├── SerializeCollectionAttribute.cs # Collection configuration
│   └── SerializePolymorphicAttribute.cs # Polymorphic type handling
├── FourSer.Gen/                # Source generator implementation (core logic)
│   ├── SerializerGenerator.cs     # Main generator entry point
│   ├── CodeGenerators/            # Code generation logic
│   │   ├── SerializationGenerator.cs    # Serialize() method generation
│   │   ├── DeserializationGenerator.cs  # Deserialize() method generation
│   │   └── PacketSizeGenerator.cs       # GetPacketSize() method generation
│   └── Models/                    # Data models for generation
├── FourSer.Consumer/           # Integration tests and examples
│   ├── UseCases/                  # Example packet definitions (check here for usage patterns)
│   ├── Extensions/                # Span read/write extensions
│   └── Program.cs                 # Test runner
├── FourSer.Analyzers/          # Roslyn analyzers for validation
└── FourSer.Gen.Nuget/          # NuGet package configuration
```

### Tests Structure
```
tests/
├── FourSer.Tests/                    # Unit tests for source generator
│   ├── GeneratorTests.cs              # Core generator tests with snapshot validation
│   └── GeneratorTestCases/            # Test input files for generator
├── FourSer.Analyzers.Test/           # Tests for Roslyn analyzers
└── Serializer.Package.Tests/         # NuGet package integration tests
```

## Common Tasks

### Frequently Referenced Files
Based on `ls -la` repository root:
```
.github/workflows/ci.yml    # CI/CD pipeline configuration
ForSer.Gen.sln             # Main solution file  
README.md                  # Comprehensive documentation with examples
Directory.Build.props      # Global MSBuild properties
Directory.Packages.props   # Central package management
```

### Key Build Files
- **Solution**: `ForSer.Gen.sln` (not FourSer.Gen.sln)
- **CI Pipeline**: `.github/workflows/ci.yml` - Shows exact build steps used in CI
- **Package Config**: `src/FourSer.Gen.Nuget/FourSer.Gen.Nuget.csproj` - NuGet package definition

### Adding New Features Workflow
1. **Update generator logic** in `src/FourSer.Gen/SerializerGenerator.cs`
2. **Add corresponding attributes** in `src/FourSer.Contracts/`
3. **Create test cases** in `src/FourSer.Consumer/UseCases/`
4. **Add unit tests** in `tests/FourSer.Tests/GeneratorTestCases/`
5. **Run validation**: Build + test + consumer + format
6. **Update documentation** in `README.md` if needed

### Debug Generator Issues
- **Check generated code**: Look at test output in `tests/FourSer.Tests/GeneratorTestCases/*/`
- **Test with Consumer**: Add test case to `src/FourSer.Consumer/UseCases/` and run
- **Unit test validation**: Use `tests/FourSer.Tests/GeneratorTests.cs` for compilation verification

## Important Notes

- **Target Framework**: .NET 9 (net9.0) - older SDKs will fail
- **Language Version**: C# 12 with preview features enabled
- **Central Package Management**: Uses `Directory.Packages.props` for version control
- **Source Generator**: Uses Roslyn source generators for compile-time code generation
- **Testing**: Uses xUnit with Verify framework for snapshot testing
- **Binary Format**: Little-endian byte order, UTF-8 strings with length prefixes
- **Performance**: Zero-allocation serialization using Span&lt;byte&gt; and ReadOnlySpan&lt;byte&gt;

## Do Not

- **DO NOT try to build without .NET 9 SDK** - it will fail with NETSDK1045 errors
- **DO NOT cancel builds** - they complete quickly (&lt;35 seconds total)
- **DO NOT skip dotnet format** - formatting issues will cause CI failures
- **DO NOT modify generated code** - edit the source generator instead
- **DO NOT add new dependencies** without updating Directory.Packages.props

## Build Failure Troubleshooting

- **NETSDK1045 error**: Install .NET 9 SDK as shown above
- **Formatting errors**: Run `dotnet format --no-restore`
- **Generator compilation errors**: Check `tests/FourSer.Tests/` for compilation validation
- **Consumer test failures**: 2 known failing tests are acceptable, others should pass
- **Package test failures**: Ensure `out/` directory exists from dotnet pack step