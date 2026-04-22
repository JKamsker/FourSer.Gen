namespace FourSer.Tests.OptimizationTesting;

internal static class OptimizationLevels
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
