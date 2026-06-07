namespace FourSer.Gen.Benchmark.Simple;

internal sealed class BenchmarkCaseLoader
{
    private readonly string _testCaseDirectory;

    public BenchmarkCaseLoader()
    {
        _testCaseDirectory = ResolveTestCaseDirectory();
    }

    public string Load(string caseName)
    {
        var inputPath = Path.Combine(_testCaseDirectory, caseName, "input.cs");
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Benchmark case '{caseName}' was not found.", inputPath);
        }

        return File.ReadAllText(inputPath);
    }

    private static string ResolveTestCaseDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tests", "FourSer.Tests", "GeneratorTestCases");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate tests/FourSer.Tests/GeneratorTestCases.");
    }
}
