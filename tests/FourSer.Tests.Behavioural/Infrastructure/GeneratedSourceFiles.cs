namespace FourSer.Tests.Behavioural.Infrastructure;

internal static class GeneratedSourceFiles
{
    public static string Read(string fileName)
    {
        var projectDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            ".."));
        var filePath = Path.Combine(
            projectDirectory,
            "Generated",
            "FourSer.Gen",
            "FourSer.Gen.SerializerGenerator",
            fileName);

        return File.ReadAllText(filePath);
    }
}
