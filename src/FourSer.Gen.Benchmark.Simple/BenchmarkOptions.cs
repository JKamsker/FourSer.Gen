namespace FourSer.Gen.Benchmark.Simple;

internal sealed class BenchmarkOptions
{
    public IReadOnlyList<string> Levels { get; init; } = BenchmarkLevels.All;

    public IReadOnlyList<string> Cases { get; init; } = ["SimplePacket"];

    public int GenerationIterations { get; init; } = 5;

    public int RuntimeIterations { get; init; } = 1_000;

    public static BenchmarkOptions Parse(string[] args)
    {
        var levels = BenchmarkLevels.All;
        var cases = new List<string> { "SimplePacket" };
        var generationIterations = 5;
        var runtimeIterations = 1_000;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--levels=", StringComparison.OrdinalIgnoreCase))
            {
                levels = SplitList(arg);
                continue;
            }

            if (arg.StartsWith("--cases=", StringComparison.OrdinalIgnoreCase))
            {
                cases = SplitList(arg).ToList();
                continue;
            }

            if (arg.StartsWith("--generation-iterations=", StringComparison.OrdinalIgnoreCase))
            {
                generationIterations = ParsePositiveInt(arg, "--generation-iterations=");
                continue;
            }

            if (arg.StartsWith("--runtime-iterations=", StringComparison.OrdinalIgnoreCase))
            {
                runtimeIterations = ParsePositiveInt(arg, "--runtime-iterations=");
            }
        }

        return new BenchmarkOptions
        {
            Levels = levels,
            Cases = cases,
            GenerationIterations = generationIterations,
            RuntimeIterations = runtimeIterations,
        };
    }

    private static IReadOnlyList<string> SplitList(string argument)
    {
        var values = argument[(argument.IndexOf('=') + 1)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return values.Length == 0 ? BenchmarkLevels.All : values;
    }

    private static int ParsePositiveInt(string argument, string prefix)
    {
        var rawValue = argument[prefix.Length..];
        return int.TryParse(rawValue, out var value) && value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(argument), $"Argument '{prefix}' must be a positive integer.");
    }
}

internal static class BenchmarkLevels
{
    public const string Off = "Off";
    public const string Conservative = "Conservative";
    public const string AggressivePortable = "AggressivePortable";
    public const string AggressiveNativeLayout = "AggressiveNativeLayout";

    public static IReadOnlyList<string> All { get; } =
    [
        Off,
        Conservative,
        AggressivePortable,
        AggressiveNativeLayout,
    ];
}
